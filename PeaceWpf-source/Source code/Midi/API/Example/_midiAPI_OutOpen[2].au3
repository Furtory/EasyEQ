#include <midiApi.au3>

Example()

Func Example()
	Local $hGUI, $hDevice

	$hGUI = GUICreate("dummy")
	GUIRegisterMsg($MM_MOM_OPEN, "MidiCallback")
	GUIRegisterMsg($MM_MOM_CLOSE, "MidiCallback")

	_midiAPI_Startup()

	$hDevice = _midiAPI_OutOpen(0, $hGUI, 1, $CALLBACK_WINDOW)
	Sleep(100) ;Give callback a chance to fire.

	_midiAPI_OutClose($hDevice)
	Sleep(100) ;Give callback a chance to fire.

	_midiAPI_Shutdown()
	GUIDelete($hGUI)
EndFunc   ;==>Example

Func MidiCallback($hWnd, $iMsg, $wParam, $lParam)
	#forceref $hWnd, $iMsg, $wParam, $lParam

	Static $tCaps = DllStructCreate($tag_midioutcaps)

	Switch $iMsg
		Case $MM_MOM_OPEN
			_midiAPI_OutGetDevCaps($wParam, DllStructGetPtr($tCaps), DllStructGetSize($tCaps))
			MsgBox(0, "Opened device:", DllStructGetData($tCaps, "szPname"))

		Case $MM_MOM_CLOSE
			MsgBox(0, "Closed device:", "Handle: " & $wParam)
	EndSwitch
EndFunc   ;==>MidiCallback
