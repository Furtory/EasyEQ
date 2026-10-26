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

	_midi_SendControlChange($hDevice, $iChannel, $CC_MODWHEEL, 127)
	_midi_SendPitchBend($hDevice, $iChannel, 0)
	MsgBox(0, "Reset Controllers", "Press OK to reset to reset controllers")

	_midi_SendResetControllers($hDevice, $iChannel)
	MsgBox(0, "Reset Controllers", "Controllers have been reset")

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(500)

	_midi_SelectPatch($hDevice, $iChannel, $PGM_GRAND_PIANO)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

