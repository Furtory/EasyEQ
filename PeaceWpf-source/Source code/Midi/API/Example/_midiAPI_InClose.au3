#include <midiApi.au3>

Example()

Func Example()
	Local $iNumDevs, $hDevice, $tCaps

	_midiAPI_Startup()
	$iNumDevs = _midiAPI_InGetNumDevs()

	If $iNumDevs Then
		;Open the last found device
		$hDevice = _midiAPI_InOpen($iNumDevs - 1)

		$tCaps = DllStructCreate($tag_midioutcaps)
		_midiAPI_InGetDevCaps($hDevice, DllStructGetPtr($tCaps), DllStructGetSize($tCaps))
		MsgBox(0, "Opened device:", DllStructGetData($tCaps, "szPname"))

		_midiAPI_InClose($hDevice)
	Else
		MsgBox(0, "Error", "No devices were found")
	EndIf

	_midiAPI_Shutdown()
EndFunc   ;==>Example
