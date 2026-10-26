#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $asDevices

	_midi_Startup()
	$hGUI = GUICreate("dummy")

	$asDevices = _midi_EnumOutputs()
	$hDevice = _midi_OpenOutput(0, $hGUI)
	If Not @error Then
		MsgBox(0, "Midi Output", StringFormat("[%s] is now open.", $asDevices[0]))
	Else
		MsgBox(0, "Midi Output", "Error opening the device")
	EndIf

	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc
