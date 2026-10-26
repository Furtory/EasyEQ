#include <midiApi.au3>

Example()

Func Example()
	Local $hDevice, $iShortMsg, $iNumNotes = 25
	Local $tCaps, $sDevName, $iCapFlags, $bVolumeSupp, $bLRVolumeSupp
	Local $iMonoVol, $iLVol, $iRVol, $iLevels
	Local Const $VOL_MAX = 0xFFFF

	_midiAPI_Startup()
	$hDevice = _midiAPI_OutOpen(0)

	;Get device name & volume control capabilities
	$tCaps = DllStructCreate($tag_midioutcaps)
	_midiAPI_OutGetDevCaps($hDevice, DllStructGetPtr($tCaps))
	$sDevName = DllStructGetData($tCaps, "szPname")
	$iCapFlags = DllStructGetData($tCaps, "dwSupport")

	$bVolumeSupp = BitAND($iCapFlags, $MIDICAPS_VOLUME)
	$bLRVolumeSupp = BitAND($iCapFlags, $MIDICAPS_LRVOLUME)

	MsgBox(0, "Volume Control Support", _
			$sDevName & @CRLF & @CRLF & _
			"MIDICAPS_VOLUME: " & @TAB & ($bVolumeSupp = True) & @CRLF & _
			"MIDICAPS_LRVOLUME: " & @TAB & ($bLRVolumeSupp = True))

	If $bVolumeSupp Then
		For $i = 0 To $iNumNotes
			If $bLRVolumeSupp Then

				;gradually increase left channel to 80%
				$iLVol = Int(($i / $iNumNotes * 0.8) * $VOL_MAX)

				;gradually decrease right channel to 20%
				$iRVol = Int((1 - $i / $iNumNotes * 0.8) * $VOL_MAX)

				;set the levels
				$iLevels = BitShift($iRVol, -16)
				$iLevels = BitOR($iLevels, $iLVol)

			Else
				;mono. gradually increase volume to 80%
				$iMonoVol = Int(($i / $iNumNotes * 0.8) * $VOL_MAX)

				;Best to set the right channel as well.
				;The device may be lying about L/R support.
				$iLevels = BitShift($iMonoVol, -16)
				$iLevels = BitOR($iLevels, $iMonoVol)
			EndIf

			_midiAPI_OutSetVolume($hDevice, $iLevels)

			;note on
			$iShortMsg = BitOR(Binary("0x900058"), BitShift(0x34 + $i, -8))
			_midiAPI_OutShortMsg($hDevice, $iShortMsg)
			Sleep(150)

			;note off
			$iShortMsg = BitAND($iShortMsg, Binary("0xFFFF00"))
			_midiAPI_OutShortMsg($hDevice, $iShortMsg)
		Next

		;report the current levels.
		$iLevels = _midiAPI_OutGetVolume($hDevice)

		$iLVol = BitAND($iLevels, 0xFFFF)
		$iRVol = BitAND(BitShift($iLevels, 16), 0xFFFF)

		MsgBox(0, "Levels", StringFormat("Mono, Left: [0x%04X]\tRight: [0x%04X]", $iLVol, $iRVol))

	Else
		MsgBox(0, "Error", "Volume control is not supported.")
	EndIf

	_midiAPI_OutClose($hDevice)
	_midiAPI_Shutdown()
EndFunc   ;==>Example

