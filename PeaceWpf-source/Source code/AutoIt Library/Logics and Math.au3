; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.30
; Description....: Logics and Math Functions
; Author(s)......: Peter Verbeek
; Version history: 1.30 	improved: _InList() may have 15 arguments
;                  1.27		added: _BitsToNumber() to convert a binary bit string to a decimal or string number
;                  			added: _NumberToBits() to convert a binary bit string to decimal or string number
;                  1.25		added: _Decimals() to get the decimal fraction of a number, converting strings to number
;                  			added: _BitSet to set/unset a bit in a number e.g. byte, word
;                  			added: _IsBit() to test a bit in a number e.g. byte is set, short for BitAnd( 2^($iBit-1), $nNumber ) > 0 ? 1 : 0
;                  1.22		added: _LogX() to calculate logarithm base X of given value
;                  1.13		added: _BitTest() to check if a bit is set
;                  1.00		initial version
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;	_InList
;	_Between
;	_BitSet
;	_BitFlip
;	_IsBit
;	_BitTest
;	_BitsToNumber
;	_NumberToBits
;	_Clamp
;	_Maximum
;	_Minimum
;	_Average
;	_Sign
;	_Decimals
;	_Tween
;	_TweenFactor
;	_LogX
;	_Log10
;	_Log2
;	_Pi
;	_2Pi
;	_e
; ===============================================================================================================================

; List of 22 functions
;	_InList							Logics					Tests if a value is equal to one of the given ones
;	_Between						Logics					Tests if a value is between minimum and maximum values, converting strings to numbers
;	_BitSet							Logics					Returns a number e.g. byte with a certain bit set or unset
;	_BitFlip						Logics					Returns a number e.g. byte, word with a certain bit flipped: 0 -> 1 or 1 -> 0
;	_IsBit							Logics					Returns a bit in a number e.g. byte is set, short for BitAnd( 2^($iBit-1), $nNumber ) > 0 ? 1 : 0
;	_BitTest						Logics					Tests if a bit in a number e.g. byte is set, short for BitAnd( Bit, Number) > 0
;	_BitsToNumber					Conversion				Returns a decimal or string number converted from a binary bit string
;	_NumberToBits					Conversion				Returns a binary bit string converted from a decimal or string number
;	_Clamp							Mathematics				Returns clamped value between minimum and maximum values, converting strings to numbers
;	_Maximum						Mathematics				Returns maximum number, converting strings to number, array input possible
;	_Minimum						Mathematics				Returns minimum number, converting strings to number, array input possible
;	_Average						Mathematics				Returns the average of values, converting strings to number, array input possible
;	_Sign							Mathematics				Returns the sign as integer (-1, 0, 1), converting strings to number
;	_Decimals						Mathematics				Returns the decimal fraction of a number, converting strings to number
;	_Tween							Mathematics				Returns value according to given factor in given range, linear tween
;	_TweenFactor					Mathematics				Returns factor according to given value in given range, linear tween
;	_LogX							Mathematics				Returns logarithm base X of given value
;	_Log10							Mathematics				Returns logarithm base 10 of given value
;	_Log2							Mathematics				Returns logarithm base 2 of given value
;	_Pi								Mathematics				Returns pi, 3.1415926535897932384626433832795
;	_2Pi							Mathematics				Returns 2*Pi, 6.2831853071795864769252867665590
;	_e								Mathematics				Returns e, 2.71828182845904523536028747135266249

; #FUNCTION# ====================================================================================================================
; Name...........: _InList
; Description....: Tests if a value is equal to one of the given ones
; Syntax.........: _InList( $nValue1 [,$nValue2 = Default [,$nValue3 = Default [,$nValue4 = Default [,$nValue5 = Default [,$nValue6 = Default [,$nValue7 = Default [,$nValue8 = Default [,$nValue9 = Default [,$nValue10 = Default]]]]]]]]] )
; Parameters.....: $nValue					- Number or string to test
;                  $vValue1					- Number or string to compare to or array with numbers or strings to compare to
;                  $nValue2 to nValue10 	- Optional numbers or strings to compare to
; Return values..: True = Value is equal to one of the given once
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _InList($nValue,$vValue1,$nValue2 = Default,$nValue3 = Default,$nValue4 = Default,$nValue5 = Default,$nValue6 = Default,$nValue7 = Default,$nValue8 = Default,$nValue9 = Default,$nValue10 = Default,$nValue11 = Default,$nValue12 = Default,$nValue13 = Default,$nValue14 = Default,$nValue15 = Default)
	If IsArray($vValue1) Then
		For $iElement = 0 To UBound($vValue1)-1
			If Number($vValue1[$iElement]) = Number($nValue) Then Return True
		Next
		Return False
	Else
		If $nValue2 = Default Then Return $nValue = $vValue1
		If $nValue3 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2
		If $nValue4 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3
		If $nValue5 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4
		If $nValue6 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5
		If $nValue7 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6
		If $nValue8 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7
		If $nValue9 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8
		If $nValue10 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8 Or $nValue = $nValue9
		If $nValue11 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8 Or $nValue = $nValue9 Or $nValue = $nValue10
		If $nValue12 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8 Or $nValue = $nValue9 Or $nValue = $nValue10 Or $nValue = $nValue11
		If $nValue13 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8 Or $nValue = $nValue9 Or $nValue = $nValue10 Or $nValue = $nValue11 Or $nValue = $nValue12
		If $nValue14 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8 Or $nValue = $nValue9 Or $nValue = $nValue10 Or $nValue = $nValue11 Or $nValue = $nValue12 Or $nValue = $nValue13
		If $nValue15 = Default Then Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8 Or $nValue = $nValue9 Or $nValue = $nValue10 Or $nValue = $nValue11 Or $nValue = $nValue12 Or $nValue = $nValue13 Or $nValue = $nValue14
		Return $nValue = $vValue1 Or $nValue = $nValue2 Or $nValue = $nValue3 Or $nValue = $nValue4 Or $nValue = $nValue5 Or $nValue = $nValue6 Or $nValue = $nValue7 Or $nValue = $nValue8 Or $nValue = $nValue9 Or $nValue = $nValue10 Or $nValue = $nValue11 Or $nValue = $nValue12 Or $nValue = $nValue13 Or $nValue = $nValue14 Or $nValue = $nValue15
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Between
; Description....: Tests if a value is between minimum and maximum values, converting strings to numbers
; Syntax.........: _Between( $nValue, $MinValue, $MaxValue )
; Parameters.....: $nValue					- Number or string to test
;                  $nMinValue				- Minimum value
;                  $nMaxValue				- Maximum value
; Return values..: True = Value is between minimum and maximum values, False = Not
; Remark.........: Both parameters may be string numbers
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Between($nValue,$nMinValue,$nMaxValue)
	Return Number($nValue) >= Number($nMinValue) And Number($nValue) <= Number($nMaxValue)			; correcting for strings
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _BitSet
; Description....: Returns a number e.g. byte, word with a certain bit set or unset
; Syntax.........: _BitSet( $iBitPosition [,$vNumber = 0 [,$vBit = 1]] )
; Parameters.....: $iBitPosition			- Bit to set or unset ranging from bit 1 to bit n
;                  $vNumber					- Positive number such as a byte
;                  $vBit					- 0 = unset bit, 1 >= set bit
; Return values..: $vNumber with bit set or unset
; Remark.........: Both parameters may be string numbers
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _BitSet($iBitPosition,$vNumber = 0,$vBit = 1)
	If IsString($iBitPosition) Then $iBitPosition = Number($iBitPosition)
	If IsString($vNumber) Then $vNumber = Number($vNumber)
	If $vNumber = 0 Then Return $vBit >= 1 ? 2^($iBitPosition-1) : 0
	If (IsBool($vBit) And $vBit) Or (IsNumber($vBit) And $vBit >= 1) Then Return BitAnd($vNumber,2^($iBitPosition-1)) > 0 ? $vNumber : ($vNumber+2^($iBitPosition-1))
	If (IsBool($vBit) And Not $vBit) Or (IsNumber($vBit) And $vBit = 0) Then Return BitAnd($vNumber,2^($iBitPosition-1)) > 0 ? ($vNumber-2^($iBitPosition-1)) : $vNumber
	Return SetError(1,0,$vNumber)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _BitFlip
; Description....: Returns a number e.g. byte, word with a certain bit flipped: 0 -> 1 or 1 -> 0
; Syntax.........: _BitFlip( $vNumber, $iBitPosition )
; Parameters.....: $vNumber					- Positive number such as a byte
;                  $iBitPosition			- Bit to flip ranging from bit 1 to bit n
; Return values..: $vNumber with flipped bit
; Remark.........: Both parameters may be string numbers
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _BitFlip($vNumber,$iBitPosition)
	If IsString($vNumber) Then $vNumber = Number($vNumber)
	If IsString($iBitPosition) Then $iBitPosition = Number($iBitPosition)
	Return BitXOR($vNumber,2^($iBitPosition-1))
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _IsBit
; Description....: Returns a bit in a number e.g. byte is set, short for BitAnd( 2^($iBit-1), $nNumber ) > 0 ? 1 : 0
; Syntax.........: _IsBit( $iBitPosition, $vNumber )
; Parameters.....: $iBitPosition			- Bit to test ranging from bit 1 to bit n
;                  $nNumber					- Positive number such as a byte
; Return values..: Bit 0 or 1
; Remark.........: Both parameters may be string numbers
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _IsBit($iBitPosition,$vNumber)
	If IsString($iBitPosition) Then $iBitPosition = Number($iBitPosition)
	If IsString($vNumber) Then $vNumber = Number($vNumber)
	Return BitAnd(2^($iBitPosition-1),$vNumber) > 0 ? 1 : 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _BitTest
; Description....: Tests if a bit in a number e.g. byte is set, short for BitAnd(Bit, Number) > 0
; Syntax.........: _BitTest( $iBit, $nNumber )
; Parameters.....: $iBit					- Bit to test 2^$iBit such as 2 or 64
;                  $nNumber					- Number such as a byte
; Return values..: True = Bit is set, False = Not set
; Remark.........: Both parameters may be string numbers
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _BitTest($iBitPosition,$vNumber)
	If IsString($iBitPosition) Then $iBitPosition = Number($iBitPosition)
	If IsString($vNumber) Then $vNumber = Number($vNumber)
	Return BitAnd($iBitPosition,$vNumber) > 0
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _BitsToNumber
; Description....: Returns a decimal or string number converted from a binary bit string
; Syntax.........: _BitsToNumber( $sBits [, $nType = 0 ] )
; Parameters.....: $sBits					- Bits to convert
;                  $nType					- Type to return: 0 = decimal (default), 1 = string number
; Return values..: Decimal or string number according to $nType
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _BitsToNumber($sBits,$nType = 0)
	Local $nDecimal = 0,$nLength = StringLen($sBits)
	For $nBit = 1 To $nLength
		If StringMid($sBits,$nBit,1) = "1" Then $nDecimal += 2^($nLength-$nBit)
	Next
	Return $nType =  0 ? $nDecimal : String($nDecimal)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _NumberToBits
; Description....: Returns a binary bit string converted from a decimal or string number
; Syntax.........: _NumberToBits( $vNumber [, $nBits = 16] )
; Parameters.....: $vNumber					- Number to convert: decimal number or string number
;                  $nBits					- Number of output bits, default 16 bits
; Return values..: Binary bits string of $nBits
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _NumberToBits($vNumber,$nBits = 16)
	Local $sBits = ""
	If IsString($vNumber) Then $vNumber = Number($vNumber)
	For $nBit = $nBits-1 To 0 Step -1
		If BitAnd($vNumber,2^$nBit) > 0 Then
			$sBits &= "1"
			$vNumber -= 2^$nBit
		Else
			$sBits &= "0"
		EndIf
	Next
	Return $sBits
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Clamp
; Description....: Returns clamped value between minimum and maximum values, converting strings to number
; Syntax.........: _Clamp($nValue, $MinValue, $MaxValue)
; Parameters.....: $nValue					- Number or string to clamp
;                  $nMinValue				- Minimum value
;                  $nMaxValue				- Maximum value
; Return values..: Clamped value between minimum and maximum values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Clamp($nValue,$nMinValue,$nMaxValue)
	If Number($nValue) > Number($nMaxValue) Then			; correcting for strings
		Return $nMaxValue
	ElseIf Number($nValue) < Number($nMinValue) Then		; correcting for strings
		Return $nMinValue
	Else
		Return $nValue
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Maximum
; Description....: Returns maximum number, converting strings to number, array input possible
; Syntax.........: _Maximum($vValue1 [,$vValue2 = Default])
; Parameters.....: $vValue1					- Number or string 1 or array with numbers or strings to get maximum
;                  $vValue2					- Number or string 2
; Return values..: Maximum number
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Maximum($vValue1,$vValue2 = Default)
	If IsArray($vValue1) Then
		If UBound($vValue1) <= 1 Then Return $vValue1[0]
		Local $nValue
		$nValue = Number($vValue1[0])
		For $iElement = 0 To UBound($vValue1)-2
			If Number($vValue1[$iElement+1]) >= Number($vValue1[$iElement]) Then $nValue = $vValue1[$iElement+1]
		Next
		Return $nValue
	Else
		If Number($vValue1) > Number($vValue2) Then			; correcting for strings
			Return $vValue1
		Else
			Return $vValue2
		EndIf
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Minimum
; Description....: Returns minimum number, converting strings to number, array input possible
; Syntax.........: _Minimum($vValue1 [,$nValue2 = Default])
; Parameters.....: $vValue1					- Number or string 1 or array with numbers or strings to get minimum
;                  $nValue2					- Number or string 2
; Return values..: Minimum number
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Minimum($vValue1,$vValue2 = Default)
	If IsArray($vValue1) Then
		If UBound($vValue1) <= 1 Then Return $vValue1[0]
		Local $nValue
		$nValue = Number($vValue1[0])
		For $iElement = 0 To UBound($vValue1)-2
			If Number($vValue1[$iElement+1]) <= Number($vValue1[$iElement]) Then $nValue = $vValue1[$iElement+1]
		Next
		Return $nValue
	Else
		If Number($vValue1) < Number($vValue2) Then			; correcting for strings
			Return $vValue1
		Else
			Return $vValue2
		EndIf
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Average
; Description....: Returns the average of values, converting strings to number, array input possible
; Syntax.........: _Average($vValue1 [,$nValue2 = Default [,$nValue3 = Default [,$nValue4 = Default [,$nValue5 = Default [,$nValue6 = Default [,$nValue7 = Default [,$nValue8 = Default [,$nValue9 = Default [,$nValue10 = Default]]]]]]]]])
; Parameters.....: $vValue1					- Number or string 1 or array to average
;                  $nValue2 to nValue10 	- Optional numbers or strings 2 - 10 to average
; Return values..: Average value
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Average($vValue1,$nValue2 = Default,$nValue3 = Default,$nValue4 = Default,$nValue5 = Default,$nValue6 = Default,$nValue7 = Default,$nValue8 = Default,$nValue9 = Default,$nValue10 = Default)
	If IsArray($vValue1) Then
		If UBound($vValue1) <= 1 Then Return $vValue1[0]
		Local $nValue
		$nValue = 0
		For $iElement = 0 To UBound($vValue1)-1
			$nValue += Number($vValue1[$iElement])
		Next
		Return $nValue/UBound($vValue1)
	Else
		If $nValue2 = Default Then Return $vValue1
		If $nValue3 = Default Then Return ($vValue1+$nValue2)/2
		If $nValue4 = Default Then Return ($vValue1+$nValue2+$nValue3)/3
		If $nValue5 = Default Then Return ($vValue1+$nValue2+$nValue3+$nValue4)/4
		If $nValue6 = Default Then Return ($vValue1+$nValue2+$nValue3+$nValue4+$nValue5)/5
		If $nValue7 = Default Then Return ($vValue1+$nValue2+$nValue3+$nValue4+$nValue5+$nValue6)/6
		If $nValue8 = Default Then Return ($vValue1+$nValue2+$nValue3+$nValue4+$nValue5+$nValue6+$nValue7)/7
		If $nValue9 = Default Then Return ($vValue1+$nValue2+$nValue3+$nValue4+$nValue5+$nValue6+$nValue7+$nValue8)/8
		If $nValue10 = Default Then Return ($vValue1+$nValue2+$nValue3+$nValue4+$nValue5+$nValue6+$nValue7+$nValue8+$nValue9)/9
		Return ($vValue1+$nValue2+$nValue3+$nValue4+$nValue5+$nValue6+$nValue7+$nValue8+$nValue9+$nValue10)/10
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Sign
; Description....: Returns the sign as integer (-1, 0, 1), converting strings to number
; Syntax.........: _Sign($nValue)
; Parameters.....: $nValue					- Number of string to get sign from
; Return values..: Sign of value as integer, 1 = > 0, 0 = 0, -1 = < 0
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Sign($nValue)
	If Number($nValue) = 0 Then Return 0
	If Number($nValue) > 0 Then Return 1
	Return -1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Decimals
; Description....: Returns the decimal fraction of a number, converting strings to number
; Syntax.........: _Decimals($nValue)
; Parameters.....: $nValue					- Number of string to get decimals from
; Return values..: Decimals value < 1
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Decimals($nValue)
	$nValue = Number($nValue)
	If $nValue = 0 Then Return 0
	If $nValue > 0 Then Return $nValue-Floor($nValue)
	Return $nValue-Ceiling($nValue)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Tween
; Description....: Returns value according to given factor in given range, linear tween
; Syntax.........: _Tween($nFactor, $nLowValue, $nHighValue)
; Parameters.....: $nFactor					- Factor 0 to 1, can be outside 0 to 1 if value wanted outside range
;                  $nLowValue				- Low value of tween range
;                  $nHighValue				- High value of tween range
; Return values..: Value according to given factor in given range
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Tween($nFactor,$nLowValue,$nHighValue)
	Return $nLowValue+$nFactor*($nHighValue-$nLowValue)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _TweenFactor
; Description....: Returns factor according to given value in given range, linear tween
; Syntax.........: _TweenFactor($nValue, $nLowValue, $nHighValue)
; Parameters.....: $nValue					- Value in tween range, can be outside range if factor wanted outside 0 to 1
;                  $nLowValue				- Low value of tween range
;                  $nHighValue				- High value of tween range
; Return values..: Factor 0 to 1 according to given value in given range
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _TweenFactor($nValue,$nLowValue,$nHighValue)
	Return ($nValue-$nLowValue)/($nHighValue-$nLowValue)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _LogX
; Description....: Returns logarithm base X of given value
; Syntax.........: _LogX($nValue, $nBase)
; Parameters.....: $nValue					- Input value
; Return number value				Logarithm base X of given value
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _LogX($nValue,$nBase)
	Return Log($nValue)/Log($nBase)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Log10
; Description....: Returns logarithm base 10 of given value
; Syntax.........: _Log10($nValue)
; Parameters.....: $nValue					- Input value
; Return number value				Logarithm base 10 of given value
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Log10($nValue)
    Return Log($nValue)/2.3025850929940456840179914546844
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Log2
; Description....: Returns logarithm base 2 of given value
; Syntax.........: _Log2($nValue)
; Parameters.....: $nValue					- Input value
; Return values..: Logarithm base 2 of given value
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Log2($nValue)
    Return Log($nValue)/0.69314718055994530941723212145818
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _Pi
; Description....: Returns Pi, 3.14159265358979323846264338327950288
; Syntax.........: _Pi()
; Parameters.....: None
; Return values..: Pi
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _Pi()
	Return 3.14159265358979323846264338327950288
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _2Pi
; Description....: Returns 2*Pi, 6.2831853071795864769252867665590
; Syntax.........: _2Pi()
; Parameters.....: None
; Return values..: 2*Pi
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _2Pi()
	Return 6.2831853071795864769252867665590
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _e
; Description....: Returns e, 2.71828182845904523536028747135266249
; Syntax.........: _e()
; Parameters.....: None
; Return values..: e
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _e()
	Return 2.71828182845904523536028747135266249
EndFunc