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

import androidx.annotation.NonNull;

import org.godotengine.godot.Godot;
import org.godotengine.godot.plugin.GodotPlugin;
import org.godotengine.godot.plugin.SignalInfo;
import org.godotengine.godot.plugin.UsedByGodot;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

/**
 * Local play without the phone's settings (docs/GDD.md "Local rooms"). The host starts a game hotspot (Android's
 * LocalOnlyHotspot, which tells the app its name and password); a friend's phone joins it from inside the game
 * (WifiNetworkSpecifier: Android asks the friend once) after reading it from the host's QR code or by holding the phones
 * together (NFC: the host's phone answers as a card, ShareService, and the friend's reads it). GDScript drives it through
 * `LocalLink`; every answer comes back as a signal, every failure as a short reason word.
 */
public class KalyeahLocal extends GodotPlugin {
	/** What the host's phone answers over NFC (ShareService), or null. */
	static volatile byte[] sharePayload = null;

	private static final int PERMISSION_REQUEST = 0x4B41; // "KA"
	private static final int NFC_FLAGS = NfcAdapter.FLAG_READER_NFC_A | NfcAdapter.FLAG_READER_NFC_B
			| NfcAdapter.FLAG_READER_SKIP_NDEF_CHECK;

	private WifiManager.LocalOnlyHotspotReservation reservation;
	private boolean hotspotStarting = false;
	private ConnectivityManager.NetworkCallback wifiCallback;
	private boolean reading = false;

	public KalyeahLocal(Godot godot) {
		super(godot);
	}

	@NonNull
	@Override
	public String getPluginName() {
		return "KalyeahLocal";
	}

	@NonNull
	@Override
	public Set<SignalInfo> getPluginSignals() {
		Set<SignalInfo> signals = new HashSet<>();
		signals.add(new SignalInfo("permissions_answered", Boolean.class));
		signals.add(new SignalInfo("hotspot_started", String.class, String.class, String.class));
		signals.add(new SignalInfo("hotspot_failed", String.class));
		signals.add(new SignalInfo("hotspot_stopped"));
		signals.add(new SignalInfo("wifi_joined"));
		signals.add(new SignalInfo("wifi_failed", String.class));
		signals.add(new SignalInfo("wifi_lost"));
		signals.add(new SignalInfo("nfc_read", String.class));
		return signals;
	}

	// ---- permissions -------------------------------------------------------------------------------------------------

	/** The runtime permissions the game hotspot and joining it need on this Android version. */
	private static String[] neededPermissions() {
		if (Build.VERSION.SDK_INT >= 33) {
			return new String[] { Manifest.permission.NEARBY_WIFI_DEVICES };
		}
		return new String[] { Manifest.permission.ACCESS_COARSE_LOCATION, Manifest.permission.ACCESS_FINE_LOCATION };
	}

	@UsedByGodot
	public boolean hasPermissions() {
		Activity activity = getActivity();
		if (activity == null) {
			return false;
		}
		for (String permission : neededPermissions()) {
			if (activity.checkSelfPermission(permission) != PackageManager.PERMISSION_GRANTED) {
				return false;
			}
		}
		return true;
	}

	/** Asks Android (its own prompt); `permissions_answered` follows. Already allowed: answered at once. */
	@UsedByGodot
	public void requestPermissions() {
		Activity activity = getActivity();
		if (activity == null) {
			emitSignal("permissions_answered", false);
			return;
		}
		if (hasPermissions()) {
			emitSignal("permissions_answered", true);
			return;
		}
		runOnUiThread(() -> activity.requestPermissions(neededPermissions(), PERMISSION_REQUEST));
	}

	@Override
	public void onMainRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
		if (requestCode == PERMISSION_REQUEST) {
			emitSignal("permissions_answered", hasPermissions());
		}
	}

	/** Android 8 to 12 start a LocalOnlyHotspot only with Location switched on. */
	@UsedByGodot
	public boolean locationNeededAndOff() {
		if (Build.VERSION.SDK_INT >= 33 || Build.VERSION.SDK_INT < 28) {
			return false;
		}
		LocationManager location = (LocationManager) getActivity().getSystemService(Context.LOCATION_SERVICE);
		return location != null && !location.isLocationEnabled();
	}

	// ---- the game hotspot (host) -------------------------------------------------------------------------------------

	/** Starts the game hotspot; `hotspot_started(name, password, security)` or `hotspot_failed(reason)` follows. */
	@UsedByGodot
	public void startHotspot() {
		if (Build.VERSION.SDK_INT < 26) {
			emitSignal("hotspot_failed", "unsupported");
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
				emitSignal("hotspot_failed", "permission");
				return;
			}
			if (locationNeededAndOff()) {
				emitSignal("hotspot_failed", "location_off");
				return;
			}
			WifiManager wifi = wifiManager();
			if (wifi == null) {
				emitSignal("hotspot_failed", "unsupported");
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
						emitSignal("hotspot_stopped");
					}

					@Override
					public void onFailed(int reason) {
						hotspotStarting = false;
						emitSignal("hotspot_failed", failure(reason));
					}
				}, new Handler(Looper.getMainLooper()));
			} catch (SecurityException e) {
				hotspotStarting = false;
				String message = String.valueOf(e.getMessage()).toLowerCase();
				emitSignal("hotspot_failed", message.contains("location mode") ? "location_off" : "permission");
			} catch (IllegalStateException e) {
				// "Caller already has an active LocalOnlyHotspot request"
				hotspotStarting = false;
				emitSignal("hotspot_failed", "busy");
			}
		});
	}

	@UsedByGodot
	public void stopHotspot() {
		runOnUiThread(() -> {
			sharePayload = null;
			if (reservation != null) {
				reservation.close();
				reservation = null;
			}
		});
	}

	@UsedByGodot
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
			ssid = config.getSsid() == null ? "" : config.getSsid();
			password = config.getPassphrase() == null ? "" : config.getPassphrase();
			int type = config.getSecurityType();
			if (type == SoftApConfiguration.SECURITY_TYPE_OPEN) {
				security = "nopass";
			} else if (type == SoftApConfiguration.SECURITY_TYPE_WPA3_SAE) {
				security = "SAE";
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
		emitSignal("hotspot_started", ssid, password, security);
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
	 * Joins a friend's Wi-Fi from inside the game (Android 10 and up shows its own "connect" dialog), then sends this app's
	 * sockets over it. `wifi_joined` or `wifi_failed(reason)` follows; `wifi_lost` if it drops.
	 */
	@UsedByGodot
	public void joinWifi(String ssid, String password, String security) {
		if (Build.VERSION.SDK_INT < 29) {
			emitSignal("wifi_failed", "unsupported");
			return;
		}
		runOnUiThread(() -> {
			leaveWifiNow();
			ConnectivityManager connectivity = connectivityManager();
			if (connectivity == null) {
				emitSignal("wifi_failed", "unsupported");
				return;
			}
			NetworkRequest request;
			try {
				WifiNetworkSpecifier.Builder specifier = new WifiNetworkSpecifier.Builder().setSsid(ssid);
				if ("SAE".equals(security)) {
					specifier.setWpa3Passphrase(password);
				} else if (!"nopass".equals(security) && !password.isEmpty()) {
					specifier.setWpa2Passphrase(password);
				}
				request = new NetworkRequest.Builder()
						.addTransportType(NetworkCapabilities.TRANSPORT_WIFI)
						.removeCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
						.setNetworkSpecifier(specifier.build())
						.build();
			} catch (IllegalArgumentException e) {
				emitSignal("wifi_failed", "bad_network");
				return;
			}
			ConnectivityManager.NetworkCallback callback = new ConnectivityManager.NetworkCallback() {
				@Override
				public void onAvailable(@NonNull Network network) {
					connectivity.bindProcessToNetwork(network);
					emitSignal("wifi_joined");
				}

				@Override
				public void onUnavailable() {
					if (wifiCallback == this) {
						wifiCallback = null;
					}
					emitSignal("wifi_failed", "declined");
				}

				@Override
				public void onLost(@NonNull Network network) {
					connectivity.bindProcessToNetwork(null);
					emitSignal("wifi_lost");
				}
			};
			wifiCallback = callback;
			try {
				connectivity.requestNetwork(request, callback);
			} catch (SecurityException e) {
				wifiCallback = null;
				emitSignal("wifi_failed", "permission");
			} catch (RuntimeException e) {
				wifiCallback = null;
				emitSignal("wifi_failed", "generic");
			}
		});
	}

	/** Lets go of the friend's Wi-Fi: the phone goes back to its own networks. */
	@UsedByGodot
	public void leaveWifi() {
		runOnUiThread(this::leaveWifiNow);
	}

	private void leaveWifiNow() {
		ConnectivityManager connectivity = connectivityManager();
		if (connectivity == null) {
			return;
		}
		connectivity.bindProcessToNetwork(null);
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
	@UsedByGodot
	public String nfcState() {
		Activity activity = getActivity();
		if (activity == null) {
			return "none";
		}
		NfcAdapter adapter = NfcAdapter.getDefaultAdapter(activity);
		if (adapter == null || !activity.getPackageManager().hasSystemFeature(PackageManager.FEATURE_NFC_HOST_CARD_EMULATION)) {
			return "none";
		}
		return adapter.isEnabled() ? "on" : "off";
	}

	/** The host: a friend's phone held against this one reads `text` (ShareService). */
	@UsedByGodot
	public void nfcShare(String text) {
		sharePayload = text == null || text.isEmpty() ? null : text.getBytes(StandardCharsets.UTF_8);
	}

	@UsedByGodot
	public void nfcStopShare() {
		sharePayload = null;
	}

	/** The friend: while on, a Kalyeah phone held against this one sends `nfc_read(text)`. */
	@UsedByGodot
	public void nfcRead(boolean on) {
		reading = on;
		runOnUiThread(this::applyReader);
	}

	private void applyReader() {
		Activity activity = getActivity();
		NfcAdapter adapter = activity == null ? null : NfcAdapter.getDefaultAdapter(activity);
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
				emitSignal("nfc_read", new String(Arrays.copyOf(response, length - 2), StandardCharsets.UTF_8));
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

	// Reader mode lasts only while the activity is in front: back on when the game returns.
	@Override
	public void onMainResume() {
		if (reading) {
			applyReader();
		}
	}

	@Override
	public void onMainDestroy() {
		sharePayload = null;
		if (reservation != null) {
			reservation.close();
			reservation = null;
		}
		leaveWifiNow();
	}

	private WifiManager wifiManager() {
		Activity activity = getActivity();
		return activity == null ? null : (WifiManager) activity.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
	}

	private ConnectivityManager connectivityManager() {
		Activity activity = getActivity();
		return activity == null ? null : (ConnectivityManager) activity.getSystemService(Context.CONNECTIVITY_SERVICE);
	}
}
