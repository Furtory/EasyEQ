#include <midi.au3>

Example()

Func Example()
	Local $hGUI, $hDevice, $iChannel = 1

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _Midi_OpenOutput(0, $hGUI)

	PlayNotes($hDevice, $iChannel)

	MsgBox(0, "Channel Fine Tuning", "Press OK tune down 50 cents.")
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, $RPN_FINE_TUNING)
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_DATA, 0x1000)
	_midi_Send14bitCtrlChange($hDevice, $iChannel, $CC_RPN, $RPN_NULL)

	PlayNotes($hDevice, $iChannel)
	Sleep(200)

	_midi_SetChanFineTuning($hDevice, $iChannel)
	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func PlayNotes($hDevice, $iChannel)
	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $iVelocity = 0x60

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], $iVelocity)
		Sleep(200)
	Next

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(500)
EndFunc
