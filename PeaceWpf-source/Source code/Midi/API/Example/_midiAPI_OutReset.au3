#include <midiApi.au3>

Example()

Func Example()
	Local $hDevice, $iShortMsg

	_midiAPI_Startup()
	$hDevice = _midiAPI_OutOpen($MIDI_MAPPER)

	;turn some notes on
	For $i = 0x3C To 0x4C
		$iShortMsg = BitOR(Binary("0x900058"), BitShift($i, -8))
		_midiAPI_OutShortMsg($hDevice, $iShortMsg)
	Next

	MsgBox(0, "midiOutReset", "Press OK to reset output device")
	_MidiAPI_OutReset($hDevice)

	MsgBox(0, "midiOutReset", "Reset performed")

	_midiAPI_OutClose($hDevice)
	_midiAPI_Shutdown()
EndFunc   ;==>Example

