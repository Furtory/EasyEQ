#include-once
#include <StringConstants.au3>
#include <String.au3>

; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.27
; Description....: String and File String Functions
; Author(s)......: Peter Verbeek
; Version history: 1.30		added: _StringSwapParts() for swapping parts of a string on a given cut position
;                  			added: _FileProperName() for stripping illegal characters from file name
;                  1.27		added: _StringZero() for zero padding with given width of a number after converting it to a string
;                  1.24		changed: _StringTidyUp() rewritten. It didn't work properly
;                  			improved: _StringTidyUp() has a second argument for not stripping leading spaces
;                  1.20		added: _StringStartsWith() for testing if a string starts with a string
;                  			added: _StringEndsWith() for testing if a string ends with a string
;                  1.19		added: _FileComparePaths() for comparing (url) paths (case-insensitive, last slash-insensitive)
;                  			added: for _FilePath() $bToLower parameter to convert path to lower case for easy comparing
;                  			bug repair: _FilePath() returned last forward slash for url paths
;                  			added: for _StringToArray() $cConvert to convert column according to format: c = string, b = boolean, n = number, d = binary
;                  			bug repair: _StringToArray() returned not always all columns
;                  1.00		initial version
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;	_StringZero
;	_StringEmpty
;	_StringInCharacters
;	_StringStartsWith
;	_StringEndsWith
;	_StringCut
;	_StringSwapParts
;	_StringStripCharacters
;	_StringDiffer
;	_StringRemoveDoubleSpaces
;	_StringCapitalize
;	_StringCount
;	_StringCountLines
;	_StringToTag
;	_StringFromTag
;	_StringToLastTag
;	_StringFromLastTag
;	_StringTidyUp
;	_StringIndent
;	_StringUnindent
;	_StringTotalUnindent
;	_StringIndentCount
;	_StringToArray
;	_StringLines
;	_XMLHasTag
;	_XMLGetString
;	_XMLSetString
;	_XMLStringToString
;	_StringToXMLString
;	_FileName
;	_FileExtension
;	_FileSetExtension
;	_FileAddExtension
;	_FilePath
;	_FileSetPath
;	_FileAddPath
;	_FileComparePaths
; ===============================================================================================================================

; List of 38 functions
;	_StringZero						String manipulation				Returns string of a number zero padded to given width
;	_StringEmpty					String manipulation				Tests if string is empty after stripping spaces, tabs, line feeds or carriage returns
;	_StringInCharacters				String manipulation				Tests if any given characters is in string
;	_StringStartsWith				String manipulation				Tests if string is starting with a string
;	_StringEndsWith					String manipulation				Tests if string is ending with a string
;	_StringCut						String manipulation				Returns string between two positions
;	_StringSwapParts				String manipulation				Returns string of swapped parts on a given cut position
;	_StringStripCharacters			String manipulation				Returns string stripped from characters
;	_StringDiffer					String manipulation				Returns position where two strings begin to differ
;	_StringRemoveDoubleSpaces		String manipulation				Returns string with removed double spaces
;	_StringCapitalize				String manipulation				Returns capitalized string (uppercase first letter)
;	_StringCount					String manipulation				Returns number of times string is found in a string
;	_StringCountLines				String manipulation				Returns number of lines in a string, 0 = none
;	_StringToTag					String manipulation				Returns string to given tag/delimiter
;	_StringFromTag					String manipulation				Returns string from given tag/delimiter
;	_StringToLastTag				String manipulation				Returns string to last found given tag/delimiter
;	_StringFromLastTag				String manipulation				Returns string from last found given tag/delimiter
;	_StringTidyUp					String text manipulation		Returns text in string stripped from leading and trailing spaces, trailing carriage returns and trailing line feeds
;	_StringIndent					String text manipulation		Returns text in string with lines left or right indented with given string
;	_StringUnindent					String text manipulation		Returns text in string with lines left or right unindented with given string
;	_StringTotalUnindent			String text manipulation		Returns text in string with lines totally left or right unindented with given character
;	_StringIndentCount				String text manipulation		Returns text in string with lines left or right indented once with given string and count
;	_StringToArray					String array manipulation		Returns 1D or 2D array of strings splitted by given row and column delimiters
;	_StringLines					String array manipulation		Returns lines in a string as an array
;	_XMLHasTag						XML string manipulation			Tests if XML has begin or end tag
;	_XMLGetString					XML string manipulation			Returns (converted) string between XML tag, XML character like &lt; can be automatically converted to <, etc.
;	_XMLSetString					XML string manipulation			Returns XML line with string between tag
;	_XMLStringToString				XML string manipulation			Returns XML string to string
;	_StringToXMLString				XML string manipulation			Returns XML string from string
;	_FileProperName					File string	manipulation		Returns file name stripped from illegal characters
;	_FileName						File string	manipulation		Returns file name with or without extension stripped from path, drive, etc.
;	_FileExtension					File string	manipulation		Returns file extension if any
;	_FileSetExtension				File string	manipulation		Returns file name with extension added or changed
;	_FileAddExtension				File string	manipulation		Returns file name with extension added (
;	_FilePath						File string	manipulation		Returns file (url) path without end back or forward slash
;	_FileSetPath					File string	manipulation		Returns file name with (url) path added or changed
;	_FileAddPath					File string	manipulation		Returns file name with (url) path
;	_FileComparePaths				File string manipulation		Returns true if (url) paths in given parameters are the same (case-insensitive comparison, last slash-insensitive)

; #FUNCTION# ====================================================================================================================
; Name...........: _StringZero
; Description....: Returns string of a number zero padded to given width
; Syntax.........: _StringZero
; Parameters.....: $vValue					- Number or string to pad zeros to
;                  $nWidth					- Width of returned string
; Return values..: Zero padded string
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringZero($vValue,$nWidth)
	Return StringFormat("%0" & $nWidth & "s",Number($vValue))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringEmpty
; Description....: Tests if string is empty after stripping spaces, tabs, line feeds or carriage returns
; Syntax.........: _StringEmpty($sString)
; Parameters.....: $sString					- String to test
; Return values..: True = string is empty, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringEmpty($sString)
	Return StringLen(StringReplace(StringReplace(StringReplace(StringStripWS($sString,$STR_STRIPALL),@CR,""),@LF,""),@TAB,"")) == 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringCut
; Description....: Returns string between two positions
; Syntax.........: _StringCut($sString, $nStartPosition [,$nEndPosition = 0 [,$bInner = True [,$bAsArray = False]]])
; Parameters.....: $sString					- String to cut
;                  $nStartPosition			- Start position
;                  $nEndPosition			- End position, 0 = last position
;                  $bInner					- True = innner part, False = outer parts as concatenating string or array
;                  $bAsArray				- True = return outer parts as array, False = as concatenating string
; Return values..: $bInner = true: return inner string, $bInner = false: return outer parts as string or array
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringCut($sString,$nStartPosition,$nEndPosition = 0,$bInner = True,$bAsArray = False)
	If $bInner Then
		If $nEndPosition = 0 Then $nEndPosition = StringLen($sString)
		Return StringMid($sString,$nStartPosition,$nEndPosition-$nStartPosition+1)
	ElseIf Not $bAsArray Then
		Return StringLeft($sString,$nStartPosition-1) & StringRight($sString,StringLen($sString)-$nEndPosition)
	Else
		Local $aParts[2]
		$aParts[0] = StringLeft($sString,$nStartPosition-1)
		$aParts[1] = StringRight($sString,StringLen($sString)-$nEndPosition)
		Return $aParts
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringSwapParts
; Description....: Returns string of swapped parts on a given cut position
; Syntax.........: _StringSwapParts($sString, $nCutPosition)
; Parameters.....: $sString					- String containing parts
;                  $nCutPosition			- Cut position
; Return values..: String of swapped parts
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringSwapParts($sString,$nCutPosition)
	If $nCutPosition > StringLen($sString) Then Return $sString
	Return StringTrimLeft($sString,$nCutPosition) & StringLeft($sString,$nCutPosition)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringStripCharacters
; Description....: Returns string stripped from characters
; Syntax.........: _StringStripCharacters($sString, $sCharacters [,$CaseSensitive = False])
; Parameters.....: sString					- String with characters to strip
;                  $sCharacters				- String with characters to use for stripping
;                  $CaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: String without characters in $sCharacters
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringStripCharacters($sString,$sCharacters,$bCaseSensitive = False)
	If StringLen($sCharacters) = 0 Then Return $sString
	For $iCharacterNo = 1 To StringLen($sCharacters)
		$sString = StringReplace($sString,StringMid($sCharacters,$iCharacterNo,1),"",($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE))
	Next
	Return $sString
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringInCharacters
; Description....: Tests if any given characters is in string
; Syntax.........: _StringInCharacters($sString, $sCharacters [,$CaseSensitive = False])
; Parameters.....: $sString					- String with characters to test
;                  $sCharacters				- String with characters to use for testing
;                  $CaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: True = one or more characters are in string, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringInCharacters($sString,$sCharacters,$bCaseSensitive = False)
	For $iCharacter = 1 To StringLen($sCharacters)
		If StringInStr($sString,StringMid($sCharacters,$iCharacter,1),($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE)) Then Return True
	Next
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringDiffer
; Description....: Returns position where two strings begin to differ
; Syntax.........: _StringDiffer($sString1, $sString2 [,$CaseSensitive = False])
; Parameters.....: $sString1				- String 1 for checking difference in position
;                  $sString2				- String 2 for checking difference in position
;                  $CaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: Position where strings differ, 0 = no difference
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringDiffer($sString1,$sString2,$bCaseSensitive = False)
	If StringLen($sString1) > StringLen($sString2) Then
		$iLength = StringLen($sString1)
	Else
		$iLength = StringLen($sString2)
	EndIf
	For $iCharacter = 1 To $iLength
		If $bCaseSensitive Then
			If Not (StringMid($sString1,$iCharacter,1) == StringMid($sString2,$iCharacter,1)) Then Return $iCharacter
		Else
			If StringMid($sString1,$iCharacter,1) <> StringMid($sString2,$iCharacter,1) Then Return $iCharacter
		EndIf
	Next
	Return 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringRemoveDoubleSpaces
; Description....: Returns string with removed double spaces
; Syntax.........: _StringRemoveDoubleSpaces($sString)
; Parameters.....: $sString					- String with double spaces to remove
; Return values..: Double spaces removed string
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringRemoveDoubleSpaces($sString)
	While StringInStr($sString, "  ") > 0
		$sString = StringReplace($sString,"  "," ")
	WEnd
	Return $sString
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringCapitalize
; Description....: Returns capitalized string (uppercase first letter)
; Syntax.........: _StringCapitalize($sString)
; Parameters.....: $sString					- String to capitalize
; Return values..: Capitalized string
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringCapitalize($sString)
	Return StringUpper(StringLeft($sString,1)) & StringMid($sString,2)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringCount
; Description....: Returns number of times string is found in a string
; Syntax.........: _StringCount($sString, $sCountString)
; Parameters.....: $sString					- String containing string to count
;                  $sCountString			- String to count
; Return values..: Number of times string is found
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringCount($sString,$sCountString)
	StringReplace($sString,$sCountString,$sCountString)
	Return @extended
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringCountLines
; Description....: Returns number of lines in a string, 0 = none
; Syntax.........: _StringCountLines($sString)
; Parameters.....: $sString					- String containing lines to count
; Return values..: Number of lines, 0 = none
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringCountLines($sString)
	If StringLen($sString) = 0 Then Return 0
	StringReplace($sString,@CRLF,@CRLF)
	Return @extended+1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringToTag
; Description....: Returns string to given tag/delimiter
; Syntax.........: _StringToTag($sString, $sTag [,$bNotFound = False [,$bCaseSensitive = False]])
; Parameters.....: $sString					- String with given tags
;                  $sTag					- Tag/delimiter
;                  $bNotFound				- True = Return $String if not found, False = return empty string
;                  $bCaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: String to given tag or if tag not found according to $NotFound
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringToTag($sString,$sTag,$bNotFound = True,$bCaseSensitive = False)
	If StringInStr($sString,$sTag,($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE)) = 0 Then Return ($bNotFound) ? ($sString) : ("")
	Return StringLeft($sString,StringInStr($sString,$sTag,($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE))-1)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringFromTag
; Description....: Returns string from given tag/delimiter
; Syntax.........: _StringFromTag($sString, $sTag [,$bNotFound = False [,$bCaseSensitive = False]])
; Parameters.....: $sString					- String with given tags
;                  $sTag					- Tag/delimiter
;                  $bNotFound				- True = Return $String if not found, False = return empty string
;                  $bCaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: String from given tag or if tag not found according to $NotFound
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringFromTag($sString,$sTag,$bNotFound = False,$bCaseSensitive = False)
	If StringInStr($sString,$sTag,($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE)) = 0 Then Return ($bNotFound) ? ($sString) : ("")
	Return StringTrimLeft($sString,StringInStr($sString,$sTag,($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE))-1+StringLen($sTag))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringToLastTag
; Description....: Returns string to last found given tag/delimiter
; Syntax.........: _StringToLastTag($sString, $sTag [,$bNotFound = False [,$bCaseSensitive = False]])
; Parameters.....: $sString					- String with given tags
;                  $sTag					- Tag/delimiter
;                  $bNotFound				- True = Return $String if not found, False = return empty string
;                  $bCaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: String to last given tag or if tag not found according to $NotFound
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringToLastTag($sString,$sTag,$bNotFound = True,$bCaseSensitive = False)
	If StringInStr($sString,$sTag,($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE)) = 0 Then Return ($bNotFound) ? ($sString) : ("")
	Return StringTrimRight($sString,StringLen(_StringFromLastTag($sString,$sTag))+StringLen($sTag))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringFromLastTag
; Description....: Returns string from last found given tag/delimiter
; Syntax.........: _StringFromLastTag($sString, $sTag [,$bNotFound = False [,$bCaseSensitive = False]])
; Parameters.....: $sString					- String with given tags
;                  $sTag					- Tag/delimiter
;                  $bNotFound				- True = Return $String if not found, False = return empty string
;                  $bCaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: String from last given tag or if tag not found according to $NotFound
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringFromLastTag($sString,$sTag,$bNotFound = False,$bCaseSensitive = False)
	If StringInStr($sString,$sTag,($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE)) = 0 Then Return ($bNotFound) ? ($sString) : ("")
	While StringInStr($sString,$sTag) > 0
		$sString = StringTrimLeft($sString,StringInStr($sString,$sTag,($bCaseSensitive) ? ($STR_CASESENSE) : ($STR_NOCASESENSE))-1+StringLen($sTag))
	WEnd
	Return $sString
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringStartsWith
; Description....: Tests if string is starting with a string
; Syntax.........: _StringStartsWith($sString, $sStartString [,$bCaseSensitive = False]])
; Parameters.....: $sString					- String to test
;                  $sStartString			- Starting string
;                  $bCaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: True if string starts with starting string
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringStartsWith($sString,$sStartString,$bCaseSensitive = False)
	Return $bCaseSensitive ? StringLeft($sString,StringLen($sStartString)) == $sStartString : StringLeft($sString,StringLen($sStartString)) = $sStartString
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringEndsWith
; Description....: Tests if string is ending with a string
; Syntax.........: _StringEndsWith($sString, $sEndString [,$bCaseSensitive = False]])
; Parameters.....: $sString					- String to test
;                  $sEndString				- Ending string
;                  $bCaseSensitive			- False = Case insensitive, True = Case sensitive
; Return values..: True if string starts with starting string
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringEndsWith($sString,$sEndString,$bCaseSensitive = False)
	Return $bCaseSensitive ? StringRight($sString,StringLen($sEndString)) == $sEndString : StringRight($sString,StringLen($sEndString)) = $sEndString
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringTidyUp
; Description....: Returns text in string stripped from leading and trailing spaces, trailing carriage returns and trailing line feeds
; Syntax.........: _StringTidyUp($sString)
; Parameters.....: $sString					- Text string to tidy up
;                  $bTrimLeading			- True = Trim leading spaces, False = Don't trim
; Return values..: Text string tied up
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringTidyUp($sString,$bTrimLeading = True)
	While StringRight($sString,1) = " " Or StringRight($sString,1) = @CR Or StringRight($sString,1) = @LF
		$sString = StringTrimRight($sString,1)
	WEnd
	Return $bTrimLeading ? StringStripWS($sString,$STR_STRIPLEADING) : $sString
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringIndent
; Description....: Returns text in string with lines left or right indented with given string
; Syntax.........: _StringIndent($sString [,$sIndentString = " " [,$bRightIndent = False]])
; Parameters.....: $sString					- String with text to indent
;                  $sIndentString			- String used to indent
;                  $bRightIndent			- True = Indent right side of text, False = Left side
; Return values..: String with indented text
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringIndent($sString,$sIndentString = " ",$bRightIndent = False)
	If $bRightIndent Then
		Return StringTrimRight(StringReplace($sString & @CRLF,@CRLF,$sIndentString & @CRLF),StringLen(@CRLF))
	Else
		Return StringTrimLeft(StringReplace(@CRLF & $sString,@CRLF,@CRLF & $sIndentString),StringLen(@CRLF))
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringUnindent
; Description....: Returns text in string with lines left or right unindented (if possible) with given string
; Syntax.........: _StringUnindent($sString [,$sUnindentString = " " [,$bRightUnindent = False]])
; Parameters.....: $sString					- String with text to unindent
;                  $sUnindentString			- String used to unindent
;                  $bRightUnindent			- True = Unindent right side of text, False = Left side
; Return values..: String with unindented text
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringUnindent($sString,$sUnindentString = " ",$bRightUnindent = False)
	If StringLen($sUnindentString) = 0 Then $sUnindentString = " "
	If $bRightUnindent Then
		$sString = $sString & @CRLF
		While StringLen($sUnindentString) > 0
			$sString = StringReplace($sString,$sUnindentString & @CRLF,@CRLF)
			$sUnindentString = StringTrimLeft($sUnindentString,1)
		WEnd
		Return StringTrimRight($sString,StringLen(@CRLF))
	Else
		$sString = @CRLF & $sString
		While StringLen($sUnindentString) > 0
			$sString = StringReplace($sString,@CRLF & $sUnindentString,@CRLF)
			$sUnindentString = StringTrimLeft($sUnindentString,1)
		WEnd
		Return StringTrimLeft($sString,StringLen(@CRLF))
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringTotalUnindent
; Description....: Returns text in string with lines totally left or right unindented (if possible) with given character
; Syntax.........: _StringTotalUnindent($sString [,$sUnindentCharacter = " " [,$bRightUnindent = False]])
; Parameters.....: $sString					- String with text to unindent
;                  $sUnindentCharacter		- Character used to unindent
;                  $bRightUnindent			- True = Unindent right side of text, False = Left side
; Return values..: String with unindented text
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringTotalUnindent($sString,$sUnindentCharacter = " ",$bRightUnindent = False)
	If StringLen($sUnindentCharacter) = 0 Then $UnindentCharacter = " "
	$sUnindentCharacter = StringLeft($sUnindentCharacter,1)			; no more than 1 unindent character allowed
	If $bRightUnindent Then
		$sString = $sString & @CRLF
		While StringInStr($sString,$sUnindentCharacter & @CRLF) > 0
			$sString = StringReplace($sString,$sUnindentCharacter & @CRLF,@CRLF)
		WEnd
		Return StringTrimRight($sString,StringLen(@CRLF))
	Else
		$sString = @CRLF & $sString
		While StringInStr($sString,@CRLF & $sUnindentCharacter) > 0
			$sString = StringReplace($sString,@CRLF & $sUnindentCharacter,@CRLF)
		WEnd
		Return StringTrimLeft($sString,StringLen(@CRLF))
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringIndentCount
; Description....: Returns text in string with lines left or right indented once with given character and count
; Syntax.........: _StringIndentCount($sString [,$sIndentCharacter = " " [,$iIndentCount = 1 [,$bRightIndent = False]]])
; Parameters.....: $sString					- String with text to indent once
;                  $sIndentCharacter		- String used to indent
;                  $iIndentCount			- Number of times to indent
;                  $bRightIndent			- True = Indent right side of text, False = Left side
; Return values..: String with once indented text
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringIndentCount($sString,$sIndentCharacter = " ",$iIndentCount = 1,$bRightIndent = False)
	If StringLen($sIndentCharacter) = 0 Then $sIndentCharacter = " "
	$sIndentCharacter = StringLeft($sIndentCharacter,1)			; no more than 1 unindent character allowed
	If $bRightIndent Then
		Return StringTrimRight(StringReplace(_StringTotalUnindent($sString & @CRLF,$sIndentCharacter,True),@CRLF,_StringRepeat($sIndentCharacter,$iIndentCount)& @CRLF),StringLen(@CRLF))
	Else
		Return StringTrimLeft(StringReplace(_StringTotalUnindent(@CRLF & $sString,$sIndentCharacter),@CRLF,@CRLF & _StringRepeat($sIndentCharacter,$iIndentCount)),StringLen(@CRLF))
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringToArray
; Description....: Returns 1D or 2D array of strings splitted by given row and column delimiters
; Syntax.........: _StringToArray($sString [,$sDelimiter = " " [,$sRowDelimiter = Default] [, $cConvert = ""]]])
; Parameters.....: $sString					- String to split
;                  $sDelimiter				- Delimiter, default space
;                  $sRowDelimiter			- Row delimiter for splitting to 2D array
;                  $cConvert				- Convert column according to format: c = string, b = boolean, n = number, d = binary
; Return values..: Array of splitted strings, @extended = number of rows in array
; Author.........: Peter Verbeek
; Modified.......: Peter Verbeek			- bug repair: not always all columns were returned
;                               			- added: to lower parameter
; ===============================================================================================================================
Func _StringToArray($sString,$sDelimiter = " ",$sRowDelimiter = Default,$cConvert = "")
	If $sRowDelimiter = Default Then
		Local $aStringArray[1]
		$aStringArray[0] = _StringToTag($sString,$sDelimiter)
		$sString = _StringFromTag($sString,$sDelimiter)
		While StringLen($sString) > 0
			ReDim $aStringArray[UBound($aStringArray)+1]
			$aStringArray[UBound($aStringArray)-1] = _StringToTag($sString,$sDelimiter)
			$sString = _StringFromTag($sString,$sDelimiter)
		WEnd
		If StringLen($cConvert) > 0 Then
			For $iElement = 0 To UBound($aStringArray)-1
				Switch StringLower(StringMid($cConvert,$iElement+1,1))
					Case 'n'
						$aStringArray[$iElement] = Number($aStringArray[$iElement])
					Case 'b'
						$aStringArray[$iElement] = StringLower($aStringArray[$iElement]) = "true"
					Case 'd'
						$aStringArray[$iElement] = Binary($aStringArray[$iElement])
				EndSwitch
			Next
		EndIf
		SetExtended(UBound($aStringArray))
	Else
		Local $iColumns = 0,$aRowArray = _StringToArray($sString,$sRowDelimiter)	; to array to calculate number of columns
		; calculate number of columns
		For $iRow In $aRowArray
			If _StringCount($iRow,$sDelimiter) > $iColumns Then $iColumns = _StringCount($iRow,$sDelimiter)
		Next
		Local $aStringArray[UBound($aRowArray)][$iColumns+1]
		ConsoleWrite($iColumns & " ")
		For $iRow = 0 To UBound($aRowArray)-1
			For $iColumn = 0 To $iColumns
				$aStringArray[$iRow][$iColumn] = _StringToTag($aRowArray[$iRow],$sDelimiter)
				Switch StringLower(StringMid($cConvert,$iColumn+1,1))
					Case 'n'
						$aStringArray[$iRow][$iColumn] = Number($aStringArray[$iRow][$iColumn])
					Case 'b'
						$aStringArray[$iRow][$iColumn] = StringLower($aStringArray[$iRow][$iColumn]) = "true"
					Case 'd'
						$aStringArray[$iRow][$iColumn] = Binary($aStringArray[$iRow][$iColumn])
				EndSwitch
				$aRowArray[$iRow] = _StringFromTag($aRowArray[$iRow],$sDelimiter)
			Next
		Next
		SetExtended(UBound($aStringArray))
	EndIf
	Return $aStringArray
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringLines
; Description....: Returns lines in a string as an array
; Syntax.........: _StringLines($sString)
; Parameters.....: $sString					- String with lines separated by @CRLF
; Return values..: Array of lines, @extended = number of rows in array
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringLines($sString)
	Local $aLines = _StringToArray($sString,@CRLF)
	SetExtended(@extended)			; set extended value with value of _StringToArray
	Return $aLines
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _XMLHasTag
; Description....: Tests if XML has begin or end tag
; Syntax.........: _XMLHasTag($sXML, $sTag [,$bTestBeginTag = True])
; Parameters.....: $sXML					- XML line
;                  $sTag					- Tag to test (without <> signs)
;                  $bTestBeginTag			- True = Test begin tag, False = Test end tag
; Return values..: True = Has tag, False = Not
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _XMLHasTag($sXML,$sTag,$bTestBeginTag = True)
	Return ($bTestBeginTag) ? (StringInStr($sXML,"<" & $sTag & ">") > 0) : (StringInStr($sXML,"</" & $sTag & ">") > 0)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _XMLGetString
; Description....: Returns (converted) string between XML tag, XML character like &lt; can be automatically converted to <, etc.
; Syntax.........: _XMLGetString($sXML ,$sTag [,$bConvert = True)
; Parameters.....: $sXML					- XML line with string between tag
;                  $sTag					- XML tag (without <> signs)
;                  $bConvert				- True = Convert XLM characters to normal characters, False = No conversion
; Return values..: String between XML tag
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _XMLGetString($sXML,$sTag,$bConvert = True)
	$sXML = _StringToTag(_StringFromTag($sXML,"<" & $sTag & ">"),"</" & $sTag & ">")
	If $bConvert Then
		Return _XMLStringToString($sXML)
	Else
		Return $sXML
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _XMLSetString
; Description....: Returns XML line with string between tag
; Syntax.........: _XMLSetString($sString, $sTag[,$sIndent = ""])
; Parameters.....: $sString					- String
;                  $sTag					- XML Tag (without <> signs)
;                  $sIndent					- String to indent XLM line
; Return values..: XML line with string between tag
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _XMLSetString($sString,$sTag,$sIndent = "")
	Return $sIndent & "<" & $sTag & ">" & _StringToXMLString($sString) & "</" & $sTag & ">"
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _XMLStringToString
; Description....: Returns XML string to string
; Syntax.........: _XMLStringToString($sXMLString)
; Parameters.....: $sXMLString				- XML string with string
; Return values..: String from XML string
Func _XMLStringToString($sXMLString)
	Return StringReplace(StringReplace(StringReplace(StringReplace(StringReplace($sXMLString,"&lt;","<"),"&gt;",">"),"&apos;","'"),"&quot;",'"'),"&amp;","&")
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StringToXMLString
; Description....: Returns XML string from string
; Syntax.........: _StringToXMLString($sString)
; Parameters.....: $sString					- String
; Return values..: XML string from string
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StringToXMLString($sString)
	Return StringReplace(StringReplace(StringReplace(StringReplace(StringReplace($sString,"&","&amp;"),"<","&lt;"),">","&gt;"),"'","&apos;"),'"',"&quot;")
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileProperName
; Description....: Returns file name stripped from illegal characters
; Syntax.........: _FileProperName($sFileName [,$sReplaceWith = ""])
; Parameters.....: $sFileName				- File name without path
;                  $sReplaceWith = ""		- Replacing character
; Return values..: File name without illegal characters
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileProperName($sFileName,$sReplaceWith = "")
	Return StringRegExpReplace($sFileName,'[\\\/:*?"<>|]',$sReplaceWith)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileName
; Description....: Returns file name with or without extension stripped from path, drive, etc.
; Syntax.........: _FileName($sFile [,$bWithExtension = True])
; Parameters.....: $sFile					- File with path, drive, etc.
;                  $bWithExtension = True	- True = Return with extension, False = Without
; Return values..: File name with or without extension
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileName($sFile,$bWithExtension = True)
	$sFile = _StringFromLastTag(_StringFromTag(_StringFromTag(_StringFromTag($sFile,":",True),"//",True),"/",True),"\",True)
	Return ($bWithExtension) ? ($sFile) : (_StringToLastTag($sFile,".",True))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileExtension
; Description....: Returns file extension if any
; Syntax.........: _FileExtension($sFile)
; Parameters.....: $File					- File with extension, path, drive, etc.
; Return values..: File extension if any
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileExtension($sFile)
	Return _StringFromLastTag($sFile,".")
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileSetExtension
; Description....: Returns file name with extension added or changed
; Syntax.........: _FileSetExtension($sFile, $sExtension)
; Parameters.....: 	$sFile					- File name
;                   $sExtension				- Extension to add or change
; Return values..: File name with extension
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileSetExtension($sFile,$sExtension)
	Return _StringToLastTag($sFile,".") & "." & _StringFromLastTag($sExtension,".",True)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileAddExtension
; Description....: Returns file name with extension added
; Syntax.........: _FileAddExtension($sFile, $sExtension)
; Parameters.....: $sFile					- File name
;                  $sExtension				- Extension to add
; Return values..: File name with extension
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileAddExtension($sFile,$sExtension)
	Return (StringRight($sFile,1) = ".") ? ($sFile & _StringFromLastTag($sExtension,".",True)) : ($sFile & "." & _StringFromLastTag($sExtension,".",True))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FilePath
; Description....: Returns file path without end back or forward slash
; Syntax.........: _FilePath($sFile)
; Parameters.....: $sFile					- File or url name with path
;                  $bToLower				- Convert path to lower string for easy comparing
; Return values..: File path without backslash or forward slash
; Author.........: Peter Verbeek
; Modified.......: Peter Verbeek			- bug repair: for url paths function returned with forward slash
;                               			- added: to lower parameter
; ===============================================================================================================================
Func _FilePath($sFile,$bToLower = False)
	Local $Path = ""
	If StringInStr($sFile,"\") Then
		$Path = _StringToLastTag($sFile,"\")
	ElseIf StringInStr($sFile,"/") Then
		$Path = _StringToLastTag($sFile,"/")
		$Path = StringRight($sFile,2) = ":/" ? ($sFile & "/") : $sFile		; return for instance http://
	ElseIf StringInStr($sFile,":") Then
		$Path = _StringToTag($sFile,":") & ":"								; return while deleting double :
	EndIf
	Return $bToLower ? StringLower($Path) : $Path
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileSetPath
; Description....: Returns file name with path added or changed
; Syntax.........: _FileSetPath($sFile, $sPath [,$bUrl = False])
; Parameters.....: $sFile					- File name
;                  $sPath					- Path to add or change
;                  $bUrl					- True = Url, False = File path
; Return values..: File name with path added or changed
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileSetPath($sFile,$sPath,$bUrl = False)
	If $bUrl Then
		$sPath = (StringLeft($sPath,1) = "/") ? (StringTrimLeft(StringStripWS($sPath,$STR_STRIPLEADING),1)) : (StringStripWS($sPath,$STR_STRIPLEADING))
		$sPath = (StringRight($sPath,1) = "/") ? $sPath : ($sPath & "/")
		Return $sPath & _FileName($sFile)
	Else
		$sPath = (StringLeft($sPath,1) = "\") ? (StringTrimLeft(StringStripWS($sPath,$STR_STRIPLEADING),1)) : (StringStripWS($sPath,$STR_STRIPLEADING))
		$sPath = (StringRight($sPath,1) = "\") ? $sPath : ($sPath & "\")
		Return $sPath & _FileName($sFile)
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileAddPath
; Description....: Returns file name with path added
; Syntax.........: _FileAddPath($sFile, $sPath [,$bUrl = False])
; Parameters.....: $sFile					- File name
;                  $sPath					- Path to add
;                  $bUrl					- True = Url, False = File path
; Return values..: File name with path
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileAddPath($sFile,$sPath,$bUrl = False)
	If $bUrl Then
		$sPath = (StringLeft($sPath,1) = "/") ? (StringTrimLeft(StringStripWS($sPath,$STR_STRIPLEADING),1)) : (StringStripWS($sPath,$STR_STRIPLEADING))
		Return (StringRight($sPath,1) = "/") ? ($sPath & ($sFile)) : ($sPath & "/" & _FileName($sFile))
	Else
		$sPath = (StringLeft($sPath,1) = "\") ? (StringTrimLeft(StringStripWS($sPath,$STR_STRIPLEADING),1)) : (StringStripWS($sPath,$STR_STRIPLEADING))
		Return (StringRight($sPath,1) = "\") ? ($sPath & ($sFile)) : ($sPath & "\" & _FileName($sFile))
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _FileComparePaths
; Description....: Returns true if (url) paths in given parameters are the same (case-insensitive comparison, last slash-insensitive)
; Syntax.........: _FileComparePaths($sPath1, $sPath2)
; Parameters.....: $sPath1					- Path to compare
;                  $sPath2					- Path to compare
; Return values..: True if paths are the same
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _FileComparePaths($sPath1, $sPath2)
	; delete any forward or backward ending slash
	$sPath1 = StringRight($sPath1,1) = "\" ? StringTrimRight($sPath1,1) : $sPath1
	$sPath1 = StringRight($sPath1,1) = "/" ? StringTrimRight($sPath1,1) : $sPath1
	$sPath2 = StringRight($sPath2,1) = "\" ? StringTrimRight($sPath2,1) : $sPath2
	$sPath2 = StringRight($sPath2,1) = "/" ? StringTrimRight($sPath2,1) : $sPath2
	Return StringLower($sPath1) = StringLower($sPath2)
EndFunc