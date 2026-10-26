#include <GUIConstants.au3>
#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $iChannel = 1

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _Midi_OpenOutput(0, $hGUI)

	;Omni On, Mono
	_midi_SetMidiMode($hDevice, 2)
	PlayNotes($hDevice, $iChannel)

	;Omni Off, Poly
	_Midi_SetMidiMode($hDevice, 3)
	PlayNotes($hDevice, $iChannel)

	_Midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc

Func PlayNotes($hDevice, $iChannel)
	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], 0x60)
		Sleep(200)
	Next
	Sleep(500)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(500)
EndFunc
