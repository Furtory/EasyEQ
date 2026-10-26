#include <midiApi.au3>

Example()

Func Example()
	Local $hGUI, $hDevice

	$hGUI = GUICreate("Dummy")
	GUIRegisterMsg($MM_MIM_OPEN, "MidiCallback")
	GUIRegisterMsg($MM_MIM_CLOSE, "MidiCallback")

	_midiAPI_Startup()

	If _midiAPI_InGetNumDevs() Then

		$hDevice = _midiAPI_InOpen(0, $hGUI, 1, $CALLBACK_WINDOW)
		Sleep(100) ;Give callback a chance to fire.

		_midiAPI_InClose($hDevice)
		Sleep(100) ;Give callback a chance to fire.

	Else
		MsgBox(0, "Error", "No devices were found")
	EndIf

	GUIDelete($hGUI)
	_midiAPI_Shutdown()

EndFunc   ;==>Example

Func MidiCallback($hWnd, $iMsg, $wParam, $lParam)
	#forceref $hWnd, $iMsg, $wParam, $lParam

	Static $tCaps = DllStructCreate($tag_midiincaps)

	Switch $iMsg
		Case $MM_MIM_OPEN
			_midiAPI_InGetDevCaps($wParam, DllStructGetPtr($tCaps), DllStructGetSize($tCaps))
			MsgBox(0, "Opened device:", DllStructGetData($tCaps, "szPname"))

		Case $MM_MIM_CLOSE
			MsgBox(0, "Closed device:", "Handle: " & $wParam)
	EndSwitch
EndFunc   ;==>MidiCallback
