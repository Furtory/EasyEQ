#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $sDevName

	_midi_Startup()
	$hGUI = GUICreate("dummy")

	$hDevice = _midi_OpenInput(0, $hGUI)
	If Not @error Then
		$sDevName = _midi_GetInputName($hDevice)
		MsgBox(0, "Midi Input", StringFormat("[%s] is now open.", $sDevName))
	Else
		MsgBox(0, "Midi Input", "Error opening the device")
	EndIf

	_midi_CloseInput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc
