package com.kalyeah.local;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.pm.PackageManager;
import android.location.LocationManager;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.NetworkRequest;
import android.net.wifi.SoftApConfiguration;
import android.net.wifi.WifiConfiguration;
import android.net.wifi.WifiManager;
import android.net.wifi.WifiNetworkSpecifier;
import android.nfc.NfcAdapter;
import android.nfc.Tag;
import android.nfc.tech.IsoDep;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;

/**
 * Local play without the phone's settings (docs/GDD.md "Local rooms"). The host starts a game hotspot (Android's
 * LocalOnlyHotspot, which tells the app its name and password); a friend's phone joins it from inside the game
 * (WifiNetworkSpecifier: Android asks the friend once, then bindProcessToNetwork routes game sockets over it) after reading
 * it from the host's QR code or by holding the phones together (NFC: the host's phone answers as a card, ShareService, and
 * the friend's reads it). Unity C# drives it through AndroidLocalLink via AndroidJavaObject + AndroidJavaProxy(Listener).
 */
public class KalyeahLocal {
    /** Callback contract implemented in C# via AndroidJavaProxy("com.kalyeah.local.KalyeahLocal$Listener"). */
    public interface Listener {
        void onPermissionsAnswered(boolean granted);
        void onHotspotStarted(String ssid, String password, String security);
        void onHotspotFailed(String reason);
        void onHotspotStopped();
        void onWifiJoined();
        void onWifiFailed(String reason);
        void onWifiLost();
        void onNfcRead(String text);
    }

    /** What the host's phone answers over NFC (ShareService), or null. */
    static volatile byte[] sharePayload = null;

    private static final int PERMISSION_REQUEST = 0x4B41; // "KA"
    private static final int NFC_FLAGS = NfcAdapter.FLAG_READER_NFC_A | NfcAdapter.FLAG_READER_NFC_B
            | NfcAdapter.FLAG_READER_SKIP_NDEF_CHECK;

    private final Activity activity;
    private final Listener listener;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    private WifiManager.LocalOnlyHotspotReservation reservation;
    private boolean hotspotStarting = false;
    private ConnectivityManager.NetworkCallback wifiCallback;
    private volatile boolean reading = false;

    public KalyeahLocal(Activity activity, Listener listener) {
        this.activity = activity;
        this.listener = listener;
    }

    private void runOnUiThread(Runnable action) {
        if (activity != null) {
            activity.runOnUiThread(action);
        } else {
            mainHandler.post(action);
        }
    }

    // ---- permissions -------------------------------------------------------------------------------------------------

    /** The runtime permissions the game hotspot and joining it need on this Android version. */
    private static String[] neededPermissions() {
        if (Build.VERSION.SDK_INT >= 33) {
            return new String[] { Manifest.permission.NEARBY_WIFI_DEVICES };
        }
        return new String[] { Manifest.permission.ACCESS_COARSE_LOCATION, Manifest.permission.ACCESS_FINE_LOCATION };
    }

    public boolean hasPermissions() {
        if (activity == null) {
            return false;
        }
        if (Build.VERSION.SDK_INT < 23) {
            return true;
        }
        for (String permission : neededPermissions()) {
            if (activity.checkSelfPermission(permission) != PackageManager.PERMISSION_GRANTED) {
                return false;
            }
        }
        return true;
    }

    public void requestPermissions() {
        if (activity == null) {
            if (listener != null) listener.onPermissionsAnswered(false);
            return;
        }
        if (hasPermissions()) {
            if (listener != null) listener.onPermissionsAnswered(true);
            return;
        }
        if (Build.VERSION.SDK_INT >= 23) {
            runOnUiThread(() -> activity.requestPermissions(neededPermissions(), PERMISSION_REQUEST));
        }
    }

    /** Android 8 to 12 start a LocalOnlyHotspot only with Location switched on. */
    public boolean locationNeededAndOff() {
        if (activity == null || Build.VERSION.SDK_INT >= 33 || Build.VERSION.SDK_INT < 28) {
            return false;
        }
        LocationManager location = (LocationManager) activity.getSystemService(Context.LOCATION_SERVICE);
        return location != null && !location.isLocationEnabled();
    }

    // ---- the game hotspot (host) -------------------------------------------------------------------------------------

    /** Starts the game hotspot; onHotspotStarted(ssid, password, security) or onHotspotFailed(reason) follows. */
    public void startHotspot() {
        if (Build.VERSION.SDK_INT < 26) {
            if (listener != null) listener.onHotspotFailed("unsupported");
            return;
        }
        runOnUiThread(() -> {
            if (reservation != null) {
                emitStarted(reservation);
                return;
            }
            if (hotspotStarting) {
                return;
            }
            if (!hasPermissions()) {
                if (listener != null) listener.onHotspotFailed("permission");
                return;
            }
            if (locationNeededAndOff()) {
                if (listener != null) listener.onHotspotFailed("location_off");
                return;
            }
            WifiManager wifi = wifiManager();
            if (wifi == null) {
                if (listener != null) listener.onHotspotFailed("unsupported");
                return;
            }
            hotspotStarting = true;
            try {
                wifi.startLocalOnlyHotspot(new WifiManager.LocalOnlyHotspotCallback() {
                    @Override
                    public void onStarted(WifiManager.LocalOnlyHotspotReservation started) {
                        hotspotStarting = false;
                        reservation = started;
                        emitStarted(started);
                    }

                    @Override
                    public void onStopped() {
                        hotspotStarting = false;
                        reservation = null;
                        sharePayload = null;
                        if (listener != null) listener.onHotspotStopped();
                    }

                    @Override
                    public void onFailed(int reason) {
                        hotspotStarting = false;
                        if (listener != null) listener.onHotspotFailed(failure(reason));
                    }
                }, mainHandler);
            } catch (SecurityException e) {
                hotspotStarting = false;
                String message = String.valueOf(e.getMessage()).toLowerCase();
                if (listener != null) {
                    listener.onHotspotFailed(message.contains("location mode") ? "location_off" : "permission");
                }
            } catch (IllegalStateException e) {
                hotspotStarting = false;
                if (listener != null) listener.onHotspotFailed("busy");
            }
        });
    }

    public void stopHotspot() {
        runOnUiThread(() -> {
            hotspotStarting = false;
            sharePayload = null;
            if (reservation != null) {
                reservation.close();
                reservation = null;
                if (listener != null) listener.onHotspotStopped();
            }
        });
    }

    public boolean hotspotRunning() {
        return reservation != null;
    }

    private static String failure(int reason) {
        switch (reason) {
            case WifiManager.LocalOnlyHotspotCallback.ERROR_INCOMPATIBLE_MODE:
                return "incompatible_mode";
            case WifiManager.LocalOnlyHotspotCallback.ERROR_TETHERING_DISALLOWED:
                return "tethering_disallowed";
            case WifiManager.LocalOnlyHotspotCallback.ERROR_NO_CHANNEL:
                return "no_channel";
            default:
                return "generic";
        }
    }

    @SuppressWarnings("deprecation")
    private void emitStarted(WifiManager.LocalOnlyHotspotReservation started) {
        String ssid = "";
        String password = "";
        String security = "WPA";
        if (Build.VERSION.SDK_INT >= 30) {
            SoftApConfiguration config = started.getSoftApConfiguration();
            if (config != null) {
                ssid = config.getSsid() == null ? "" : config.getSsid();
                password = config.getPassphrase() == null ? "" : config.getPassphrase();
                int type = config.getSecurityType();
                if (type == SoftApConfiguration.SECURITY_TYPE_OPEN) {
                    security = "nopass";
                } else if (type == SoftApConfiguration.SECURITY_TYPE_WPA3_SAE) {
                    security = "SAE";
                }
            }
        } else {
            WifiConfiguration config = started.getWifiConfiguration();
            if (config != null) {
                ssid = unquote(config.SSID);
                password = unquote(config.preSharedKey);
                if (password.isEmpty()) {
                    security = "nopass";
                }
            }
        }
        if (listener != null) {
            listener.onHotspotStarted(ssid, password, security);
        }
    }

    private static String unquote(String value) {
        if (value == null) {
            return "";
        }
        if (value.length() >= 2 && value.startsWith("\"") && value.endsWith("\"")) {
            return value.substring(1, value.length() - 1);
        }
        return value;
    }

    // ---- joining it (friend) -----------------------------------------------------------------------------------------

    /**
     * Joins a friend's Wi-Fi from inside the game (Android 10 and up shows its own "connect" dialog), then binds this app's
     * sockets to that network. onWifiJoined() or onWifiFailed(reason) follows; onWifiLost() if it drops.
     */
    public void joinWifi(String ssid, String password, String security) {
        if (Build.VERSION.SDK_INT < 29) {
            if (listener != null) listener.onWifiFailed("unsupported");
            return;
        }
        final String safeSsid = ssid == null ? "" : ssid;
        final String safePassword = password == null ? "" : password;
        final String safeSecurity = security == null ? "WPA" : security;
        runOnUiThread(() -> {
            leaveWifiNow();
            ConnectivityManager connectivity = connectivityManager();
            if (connectivity == null) {
                if (listener != null) listener.onWifiFailed("unsupported");
                return;
            }
            NetworkRequest request;
            try {
                WifiNetworkSpecifier.Builder specifier = new WifiNetworkSpecifier.Builder().setSsid(safeSsid);
                if ("SAE".equals(safeSecurity)) {
                    specifier.setWpa3Passphrase(safePassword);
                } else if (!"nopass".equals(safeSecurity) && !safePassword.isEmpty()) {
                    specifier.setWpa2Passphrase(safePassword);
                }
                request = new NetworkRequest.Builder()
                        .addTransportType(NetworkCapabilities.TRANSPORT_WIFI)
                        .removeCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                        .setNetworkSpecifier(specifier.build())
                        .build();
            } catch (IllegalArgumentException e) {
                if (listener != null) listener.onWifiFailed("bad_network");
                return;
            }
            ConnectivityManager.NetworkCallback callback = new ConnectivityManager.NetworkCallback() {
                @Override
                public void onAvailable(Network network) {
                    connectivity.bindProcessToNetwork(network);
                    if (listener != null) listener.onWifiJoined();
                }

                @Override
                public void onUnavailable() {
                    if (wifiCallback == this) {
                        wifiCallback = null;
                    }
                    if (listener != null) listener.onWifiFailed("declined");
                }

                @Override
                public void onLost(Network network) {
                    connectivity.bindProcessToNetwork(null);
                    if (listener != null) listener.onWifiLost();
                }
            };
            wifiCallback = callback;
            try {
                connectivity.requestNetwork(request, callback);
            } catch (SecurityException e) {
                wifiCallback = null;
                if (listener != null) listener.onWifiFailed("permission");
            } catch (RuntimeException e) {
                wifiCallback = null;
                if (listener != null) listener.onWifiFailed("generic");
            }
        });
    }

    /** Lets go of the friend's Wi-Fi: the phone goes back to its own networks. */
    public void leaveWifi() {
        runOnUiThread(this::leaveWifiNow);
    }

    private void leaveWifiNow() {
        ConnectivityManager connectivity = connectivityManager();
        if (connectivity == null) {
            return;
        }
        if (Build.VERSION.SDK_INT >= 23) {
            connectivity.bindProcessToNetwork(null);
        }
        if (wifiCallback != null) {
            try {
                connectivity.unregisterNetworkCallback(wifiCallback);
            } catch (IllegalArgumentException ignored) {
                // already released by Android
            }
            wifiCallback = null;
        }
    }

    // ---- NFC: tap to join --------------------------------------------------------------------------------------------

    /** "none" (no NFC chip, or it cannot answer as a card), "off" (switched off in settings) or "on". */
    public String nfcState() {
        if (activity == null) {
            return "none";
        }
        NfcAdapter adapter = NfcAdapter.getDefaultAdapter(activity);
        if (adapter == null || !activity.getPackageManager().hasSystemFeature(PackageManager.FEATURE_NFC_HOST_CARD_EMULATION)) {
            return "none";
        }
        return adapter.isEnabled() ? "on" : "off";
    }

    /** The host: a friend's phone held against this one reads text (ShareService). */
    public void nfcShare(String text) {
        sharePayload = text == null || text.isEmpty() ? null : text.getBytes(StandardCharsets.UTF_8);
    }

    public void nfcStopShare() {
        sharePayload = null;
    }

    /** The friend: while on, a Kalyeah phone held against this one sends onNfcRead(text). */
    public void nfcRead(boolean on) {
        reading = on;
        runOnUiThread(this::applyReader);
    }

    private void applyReader() {
        if (activity == null || Build.VERSION.SDK_INT < 19) {
            return;
        }
        NfcAdapter adapter = NfcAdapter.getDefaultAdapter(activity);
        if (adapter == null) {
            return;
        }
        if (reading) {
            adapter.enableReaderMode(activity, this::readTag, NFC_FLAGS, null);
        } else {
            adapter.disableReaderMode(activity);
        }
    }

    private void readTag(Tag tag) {
        IsoDep card = IsoDep.get(tag);
        if (card == null) {
            return;
        }
        try {
            card.connect();
            card.setTimeout(2000);
            byte[] response = card.transceive(ShareService.selectCommand());
            int length = response.length;
            if (length > 2 && response[length - 2] == (byte) 0x90 && response[length - 1] == 0x00) {
                String decoded = new String(Arrays.copyOf(response, length - 2), StandardCharsets.UTF_8);
                if (listener != null) {
                    mainHandler.post(() -> listener.onNfcRead(decoded));
                }
            }
        } catch (IOException | RuntimeException ignored) {
            // moved apart too soon: the friend tries again
        } finally {
            try {
                card.close();
            } catch (IOException ignored) {
                // nothing to do
            }
        }
    }

    /** Reader mode lasts only while the activity is in front: re-apply when Unity resumes. */
    public void onResume() {
        if (reading) {
            runOnUiThread(this::applyReader);
        }
    }

    public void onDestroy() {
        reading = false;
        sharePayload = null;
        runOnUiThread(() -> {
            applyReader();
            if (reservation != null) {
                reservation.close();
                reservation = null;
            }
            leaveWifiNow();
        });
    }

    private WifiManager wifiManager() {
        return activity == null ? null : (WifiManager) activity.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
    }

    private ConnectivityManager connectivityManager() {
        return activity == null ? null : (ConnectivityManager) activity.getSystemService(Context.CONNECTIVITY_SERVICE);
    }
}
