#include <midiApi.au3>

Example()

Func Example()
	Local $hDevice

	;Some midi short messages.
	Local $aMessages[7] = [ _
			Binary("0x903C50"), _
			Binary("0x913F50"), _
			Binary("0xE17F7F"), _
			Binary("0x904D50"), _
			Binary("0x803C00"), _
			Binary("0x804D00"), _
			Binary("0x813F00")]

	_midiAPI_Startup()
	$hDevice = _midiAPI_OutOpen(0)

	For $i = 0 To UBound($aMessages) - 1
		_midiAPI_OutShortMsg($hDevice, Int($aMessages[$i]))
		Sleep(200)
	Next

	_midiAPI_OutClose($hDevice)
	_midiAPI_Shutdown()
EndFunc   ;==>Example
