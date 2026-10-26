#include <midiApi.au3>

Example()

Func Example()
	Local $iNumDevs

	_midiAPI_Startup()

	$iNumDevs = _midiAPI_OutGetNumDevs()
	MsgBox(0, "_midiAPI_OutGetNumDevs", StringFormat("%d device(s) were found.", $iNumDevs))

	_midiAPI_Shutdown()
EndFunc   ;==>Example
