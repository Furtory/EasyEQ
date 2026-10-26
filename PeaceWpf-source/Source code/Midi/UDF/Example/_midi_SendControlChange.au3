#include <midi.au3>

Example()

Func Example()
	Local $hGUI
	Local $aiC7[4] = [0x3C, 0x40, 0x43, 0x46]
	Local $hDevice, $iChannel = 1

	_midi_Startup()
	$hGUI = GUICreate("dummy")
	$hDevice = _Midi_OpenOutput(0, $hGUI)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOn($hDevice, $iChannel, $aiC7[$i], 0x60)
	Next
	Sleep(500)

	_midi_SendControlChange($hDevice, $iChannel, $CC_MODWHEEL, 127)
	Sleep(1000)

	For $i = 0 To UBound($aiC7) - 1
		_midi_SendNoteOff($hDevice, $iChannel, $aiC7[$i])
	Next
	Sleep(500)

	_midi_SendControlChange($hDevice, $iChannel, $CC_MODWHEEL, 0)
	_Midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc


