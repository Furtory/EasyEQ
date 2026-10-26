#include <midi.au3>

Example()

Func Example()

	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $hGUI, $hDevice, $iChannel = 1, $iVelocity = 0x60

	_midi_Startup()
	$hGUI = GUICreate("Dummy")
	$hDevice = _midi_OpenOutput(0, $hGUI)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], $iVelocity)
	Next
	Sleep(500)

	For $i = 0x2000 To 0x3FFF Step 127
		_midi_SendPitchBend($hDevice, $iChannel, $i)
		Sleep(5)
	Next
	Sleep(500)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(500)

	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

