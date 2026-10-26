#include <midi.au3>

Example()

Func Example()
	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $hGUI, $hDevice, $iChannel = 1

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _midi_OpenOutput(0, $hGUI)

	_midi_SelectPatch($hDevice, $iChannel, $PGM_DBAR_ORGAN)
	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], 0x60)
	Next
	Sleep(500)

	MsgBox(0, "All sound off", "Press OK to turn all sound off.")
	_midi_SendAllSoundOff($hDevice, $iChannel)
	MsgBox(0, "All sound off", "Message sent.")

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(200)

	_midi_SelectPatch($hDevice, $iChannel, $PGM_GRAND_PIANO)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc
