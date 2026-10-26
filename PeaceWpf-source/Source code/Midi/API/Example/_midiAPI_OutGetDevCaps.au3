#include <midiApi.au3>

Example()

Func Example()
	Local $iNumDevs, $tCaps, $sMsg, $sName

	_midiAPI_Startup()

	$iNumDevs = _midiAPI_OutGetNumDevs()
	If Not $iNumDevs Then MsgBox(0, "Error", "No devices were found")

	$tCaps = DllStructCreate($tag_midioutcaps)
	For $i = 0 To $iNumDevs - 1
		_midiAPI_OutGetDevCaps($i, DllStructGetPtr($tCaps), DllStructGetSize($tCaps))

		$sName = DllStructGetData($tCaps, "szPname")
		$sMsg &= StringFormat("DevID:[%d] %s\n", $i, $sName)
	Next

	If $iNumDevs Then MsgBox(0, "Found devices", $sMsg)

	_midiAPI_Shutdown()
EndFunc   ;==>Example
