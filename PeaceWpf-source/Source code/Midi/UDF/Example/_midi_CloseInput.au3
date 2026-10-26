#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $asDevices

	_midi_Startup()
	$hGUI = GUICreate("dummy")

	$asDevices = _midi_EnumInputs()
	$hDevice = _midi_OpenInput(0, $hGUI)
	If Not @error Then
		MsgBox(0, "Midi Input", StringFormat("[%s] is now open.", $asDevices[0]))
	Else
		MsgBox(0, "Midi Input", "Error opening the device")
	EndIf

	_midi_CloseInput($hDevice)

	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc
