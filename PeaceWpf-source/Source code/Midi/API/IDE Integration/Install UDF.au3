Install()
If @error Then
	MsgBox(0, "Error", "Oops! an error occured.")
Else
	MsgBox(0, "Installed", "Please restart scite if need be.")
EndIf

Func Install()

	Local Const $sWarning = _
			"If you have existing user calltips and keywords specified for scite," & _
			"it is a VERY good idea to back them up." & @CRLF & @CRLF  & _
			"Continue?"

	If MsgBox(4, "Warning", $sWarning) <> 6 Then Exit

	Local Const $sCallTipsOutFile = "au3.user.calltips.api"
	Local Const $sKeywordsOutFile = "au3.UserUdfs.properties"
	Local Const $sOutUDFFile = "midiAPI.au3"
	Local Const $sOutUDFConstsFile = "midiAPIConstants.au3"

	Local $sNewCallTips = FileRead("midiAPICallTips.txt")
	Local $sNewKeywords = FileRead("midiAPIKeywords.txt")
	Local $sUDF = FileRead("..\midiAPI.au3")
	Local $sUDFConstants = FileRead("..\midiAPIConstants.au3")

	If Not $sNewCallTips Then Return SetError(2, 1)
	If Not $sNewKeywords Then Return SetError(2, 1)
	If Not $sUDF Then Return SetError(2, 1)
	If Not $sUDFConstants Then Return SetError(2, 1)

	Local $sSciteUserHome = EnvGet("SCITE_USERHOME")
	Local $sIncludePaths = RegRead("HKEY_CURRENT_USER\Software\AutoIt v3\AutoIt", "Include")
	If @error Then
		RegEnumKey("HKEY_CURRENT_USER\Software\AutoIt v3", 1)
		If @error Then Return SetError(2, 2)
		RegWrite("HKEY_CURRENT_USER\Software\AutoIt v3\AutoIt", "Include", "REG_SZ", "")
		If @error Then Return SetError(5)
	EndIf

	If Not FileExists($sSciteUserHome) Then Return SetError(2, 2)

	Local $sNewIncludePath = @LocalAppDataDir & "\AutoIt v3"
	If Not $sIncludePaths Then
		If Not FileExists($sNewIncludePath) Then Return SetError(2, 2)
		$sNewIncludePath &= "\Include"
		If Not FileExists($sNewIncludePath) Then
			If Not DirCreate($sNewIncludePath) Then Return SetError(5)
		EndIf
		$sIncludePaths = $sNewIncludePath
		RegWrite("HKEY_CURRENT_USER\Software\AutoIt v3\AutoIt", "Include", "REG_SZ", $sNewIncludePath)
		If @error Then Return SetError(5)
	EndIf
	$asIncludePaths = StringSplit($sIncludePaths, ";", 2)

	Local $bInstalled = False
	For $i = 0 To UBound($asIncludePaths) - 1
		If FileChangeDir($asIncludePaths[$i]) Then
			If FileExists($sOutUDFFile) Then FileDelete($sOutUDFFile)
			If FileExists($sOutUDFConstsFile) Then FileDelete($sOutUDFConstsFile)
			If Not $bInstalled Then
				If Not FileWrite($sOutUDFFile, $sUDF) Then Return SetError(5)
				If Not FileWrite($sOutUDFConstsFile, $sUDFConstants) Then Return SetError(5)
				$bInstalled = True
			EndIf
		EndIf
	Next
	If Not $bInstalled Then Return SetError(2, 2)

	If Not FileChangeDir($sSciteUserHome) Then Return SetError(2, 2)

	Local $sCurCallTips
	Local $hFile, $sLine
	If Not FileExists($sCallTipsOutFile) Then
		If Not FileWrite($sCallTipsOutFile, $sNewCallTips) Then Return SetError(5)
	Else
		$hFile = FileOpen($sCallTipsOutFile)
		If $hFile < 0 Then Return SetError(5)
		While 1
			$sLine = FileReadLine($hFile) & @CRLF
			If @error Then  ExitLoop
			If StringLen($sLine) = 2 Then ContinueLoop
			If StringLeft($sLine, 9) <> "_midiAPI_" Then $sCurCallTips &= $sLine
		WEnd
		$sCurCallTips &= $sNewCallTips
	EndIf
	FileClose($hFile)
	$hFile = FileOpen($sCallTipsOutFile, 2)
	If $hFile < 0 Then Return SetError(5)
	If Not FileWrite($hFile, $sCurCallTips) Then Return SetError(5)
	FileClose($hFile)

	Local $sCurKeywords, $bStartSect, $bStarted
	If Not FileExists($sKeywordsOutFile) Then
		If Not FileWrite($sKeywordsOutFile, $sNewKeywords) Then Return SetError(5)
	Else
		$hFile = FileOpen($sKeywordsOutFile)
		While 1
			$sLine = FileReadLine($hFile) & @CRLF
			If @error Then ExitLoop
			If StringLen($sLine) = 2 Then ContinueLoop
			If StringLeft(StringStripWS($sLine, 1), 1) = ";" Then
				$sCurKeywords &= $sLine
				ContinueLoop
			EndIf
			$sLine = StringRegExpReplace($sLine, "_midiapi_[[:graph:]]+", "")

			If StringLeft($sLine, 23) = "au3.keywords.user.udfs=" Then
				$sLine = StringTrimLeft($sLine, 23)
				$bStartSect = True
			EndIf
			If StringStripWS($sLine, 8) = "\" Then ContinueLoop
			If StringStripWS($sLine, 8) = "" Then ContinueLoop

			If $bStartSect Then
				$sLine = "au3.keywords.user.udfs=" & StringStripWS($sLine, 1)
				$bStartSect = False
				$bStarted = True
			EndIf

			$sCurKeywords &= $sLine
		WEnd
	EndIf

	FileClose($hFile)
	If Not $bStarted Then
		$sCurKeywords &= $sNewKeywords
	Else
		$sCurKeywords &= @TAB & StringTrimLeft($sNewKeywords, 23)
	EndIf

	$hFile = FileOpen($sKeywordsOutFile, 2)
	If $hFile < 0 Then Return SetError(5)
	If Not FileWrite($hFile, $sCurKeywords) Then SetError(5)
	FileClose($hFile)

EndFunc
