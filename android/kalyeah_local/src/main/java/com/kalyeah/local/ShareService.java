package com.kalyeah.local;

import android.nfc.cardemulation.HostApduService;
import android.os.Bundle;

import java.util.Arrays;

/**
 * The host's phone answering a friend's phone held against it: to Kalyeah's SELECT it replies with what the host shares (the
 * game hotspot as a Wi-Fi QR text, KalyeahLocal#nfcShare), then 90 00. With nothing shared it answers "not found".
 */
public class ShareService extends HostApduService {
	static final byte[] AID = { (byte) 0xF0, 0x4B, 0x41, 0x4C, 0x59, 0x45, 0x41 };
	private static final byte[] OK = { (byte) 0x90, 0x00 };
	private static final byte[] NOT_FOUND = { 0x6A, (byte) 0x82 };
	private static final byte[] UNKNOWN = { 0x6D, 0x00 };

	/** The SELECT by AID the reader sends: 00 A4 04 00, the AID's length, the AID, Le 00. */
	static byte[] selectCommand() {
		byte[] command = new byte[6 + AID.length];
		command[0] = 0x00;
		command[1] = (byte) 0xA4;
		command[2] = 0x04;
		command[3] = 0x00;
		command[4] = (byte) AID.length;
		System.arraycopy(AID, 0, command, 5, AID.length);
		command[5 + AID.length] = 0x00;
		return command;
	}

	static boolean isSelect(byte[] apdu) {
		if (apdu == null || apdu.length < 5 + AID.length) {
			return false;
		}
		return apdu[0] == 0x00 && apdu[1] == (byte) 0xA4 && apdu[2] == 0x04 && apdu[4] == AID.length
				&& Arrays.equals(Arrays.copyOfRange(apdu, 5, 5 + AID.length), AID);
	}

	@Override
	public byte[] processCommandApdu(byte[] apdu, Bundle extras) {
		if (!isSelect(apdu)) {
			return UNKNOWN;
		}
		byte[] payload = KalyeahLocal.sharePayload;
		if (payload == null) {
			return NOT_FOUND;
		}
		byte[] response = Arrays.copyOf(payload, payload.length + OK.length);
		System.arraycopy(OK, 0, response, payload.length, OK.length);
		return response;
	}

	@Override
	public void onDeactivated(int reason) {}
}
