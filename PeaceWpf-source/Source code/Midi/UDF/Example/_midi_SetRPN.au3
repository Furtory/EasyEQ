#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $iChannel = 1

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _midi_OpenOutput(0, $hGUI)
	Sleep(250)

	PlayNotes($hDevice, $iChannel)

	MsgBox(0, "Channel Fine Tuning", "Press OK tune down 150 cents.")
	_midi_SetRPN($hDevice, $iChannel, $RPN_COURSE_TUNING, 0x3F)
	_midi_SetRPN($hDevice, $iChannel, $RPN_FINE_TUNING, 0x1000, True)

	PlayNotes($hDevice, $iChannel)

	_midi_SetChanCourseTuning($hDevice, $iChannel)
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
