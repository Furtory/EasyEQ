#include <midi.au3>

Example()

Func Example()

	Local $hGUI, $hDevice, $sDevName

	_midi_Startup()
	$hGUI = GUICreate("dummy")

	$hDevice = _midi_OpenOutput(0, $hGUI)
	If Not @error Then
		$sDevName = _midi_GetOutputName($hDevice)
		MsgBox(0, "Midi Output", StringFormat("[%s] is now open.", $sDevName))
	Else
		MsgBox(0, "Midi Output", "Error opening the device")
	EndIf

	_midi_CloseOutput($hDevice)
	GUIDelete($hGUI)
	_midi_Shutdown()
EndFunc
