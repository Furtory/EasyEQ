#include <midi.au3>

Example()

Func Example()

	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $hGUI, $hDevice, $iChannel = 1

	_midi_RegDebugFunc("MidiDbg")

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _Midi_OpenOutput(0, $hGUI)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], 0x60)
		Sleep(200)
	Next
	Sleep(500)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
		Sleep(200)
	Next
	Sleep(500)

	_Midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func MidiDbg($hDevice, $iIO, $dMsg)
	#forceref $hDevice, $iIO, $dMsg
	Local $sPrefix = "O:"
	If $iIO = $MIDI_DBG_IO_IN Then $sPrefix = "I:"

	ConsoleWrite(StringFormat("%s %s\r\n", $sPrefix, $dMsg))
EndFunc