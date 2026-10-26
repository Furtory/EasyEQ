#include-once
#include <AutoItConstants.au3>

; #INDEX# =======================================================================================================================
; Title .........: Pal, Peter's AutoIt Library, version 1.10
; Description....: Stack Functions
; Author(s)......: Peter Verbeek
; Version history: 1.10		added: list functions
;							improved: VarGetType added for comparing same variable types in value in list/stack/shift/map functions
;                  1.00		initial version
; ===============================================================================================================================

; #CURRENT# =====================================================================================================================
;_ListCreate
;_ListIsList
;_ListIsEmpty
;_ListClear
;_ListAdd
;_ListInsert
;_ListSet
;_ListRemove
;_ListRemoveByValue
;_ListRemoveByColumnValue
;_ListGet
;_ListGetColumn
;_ListValueInList
;_ListValueInListColumn
;_ListSize
;_ListToArray
;_ArrayToList
;_StackCreate
;_StackIsStack
;_StackIsEmpty
;_StackClear
;_StackPush
;_StackPop
;_StackPeek
;_StackGet
;_StackValueInStack
;_StackSize
;_StackToArray
;_ArrayToStack
;_ShiftCreate
;_ShiftIsShift
;_ShiftIsEmpty
;_ShiftClear
;_ShiftPush
;_ShiftPop
;_ShiftPeek
;_ShiftGet
;_ShiftValueInShift
;_ShiftSize
;_ShiftToArray
;_ArrayToShift
;_MapCreate
;_MapIsMap
;_MapIsEmpty
;_MapClear
;_MapAdd
;_MapValueByKey
;_MapKeyByValue
;_MapRemoveByKey
;_MapRemoveByValue
;_MapGetKey
;_MapGetValue
;_MapKeyInMap
;_MapValueInMap
;_MapSize
;_MapToArray
;_ArrayToMap
; ===============================================================================================================================

; #INTERNAL_USE_ONLY# ===========================================================================================================
;__ListSetColumns()
; ===============================================================================================================================

; List of 57 functions
;	_ListCreate						List					Creates a 1D or 2D list for storing values, maximum 20 columns
;	_ListIsList						List					Tests if given array is a list
;	_ListIsEmpty					List					Tests if list is empty
;	_ListClear						List					Clears a list
;	_ListAdd						List					Adds an element (row) to a list
;	_ListInsert						List					Inserts an element (row) at given position into a list
;	_ListSet						List					Sets columns of an element of a list
;	_ListRemove						List					Removes an element from the list of given value
;	_ListRemoveByValue				List					Removes an element from the list of given value, value can be any column of the list
;	_ListRemoveByColumnValue		List					Removes an element from the list of given value in given column
;	_ListGet						List					Gets (array) value of given position of a list
;	_ListGetColumn					List					Gets (column) value of given position of a list
;	_ListValueInList				List					Tests if a value is any column of the list
;	_ListValueInListColumn			List					Tests if a value is in given column of the list
;	_ListSize						List					Gets list size
;	_ListToArray					List					Creates an array from the given list
;	_ArrayToList					List					Creates a list from the given array
;	_StackCreate					Stack					Creates a stack (LIFO) for pushing and popping values
;	_StackIsStack					Stack					Tests if given array is a stack
;	_StackIsEmpty					Stack					Tests if stack is empty
;	_StackClear						Stack					Clears a stack
;	_StackPush						Stack					Pushes (adds) a value onto the given stack at last position
;	_StackPop						Stack					Pops (removes) a value from the given stack at last position
;	_StackPeek						Stack					Gets the last pushed value without popping (removing)
;	_StackGet						Stack					Gets a value of a given position, zero based
;	_StackValueInStack				Stack					Tests if a value is in the given stack
;	_StackSize						Stack					Gets stack size
;	_StackToArray					Stack					Creates an array from the given stack
;	_ArrayToStack					Stack					Creates a stack from the given array
;	_ShiftCreate					Shift register			Creates a shift register (FIFO) for pushing and popping values
;	_ShiftIsShift					Shift register			Tests if given array is a shift register
;	_ShiftIsEmpty					Shift register			Tests if shift register is empty
;	_ShiftClear						Shift register			Clears a shift register
;	_ShiftPush						Shift register			Pushes (adds) a value into the given shift register on first position
;	_ShiftPop						Shift register			Pops (removes) a value from the given shift register at last position
;	_ShiftPeek						Shift register			Gets the value to pop without actual popping (removing)
;	_ShiftGet						Shift register			Gets a value of a given position, zero based
;	_ShiftValueInShift				Shift register			Tests if a value is in the given shift register
;	_ShiftSize						Shift register			Gets shift register size
;	_ShiftToArray					Shift register			Creates an array from the given shift register
;	_ArrayToShift					Shift register			Creates a shift register from the given array
;	_MapCreate						Map						Creates a key map for storing values attach to keys, can be use bidirectional
;	_MapIsMap						Map						Tests if given array is a key map
;	_MapIsEmpty						Map						Tests if key map is empty
;	_MapClear						Map						Clears a key map
;	_MapAdd							Map						Adds a key-value pair to the given key map
;	_MapValueByKey					Map						Gets a value of given key
;	_MapKeyByValue					Map						Gets a key of given value
;	_MapRemoveByKey					Map						Removes a key-value pair by key from the given key map
;	_MapRemoveByValue				Map						Removes a key-value pair by value from the given key map
;	_MapGetKey						Map						Gets a key of a given position, zero based
;	_MapGetValue					Map						Gets a value of a given position, zero based
;	_MapKeyInMap					Map						Tests if a key is in the key map
;	_MapValueInMap					Map						Tests if a value is in the key map
;	_MapSize						Map						Gets key map size
;	_MapToArray						Map						Creates an array from the given key map
;	_ArrayToMap						Map						Creates a key map from the given 2D array

; #FUNCTION# ====================================================================================================================
; Name...........: _ListCreate
; Description....: Creates a 1D or 2D list for storing values, maximum 20 columns
; Syntax.........: _ListCreate([$nColumns])
; Parameters.....: $nColumns				- Number of columns
; Return values..: List array as handle
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListCreate($nColumns = 1)
	If $nColumns = 1 Then
		Local $aList = ["{BFE065C9-D90B-49FF-9600-D9E7B2811E93}"]	; globally unique identifier of array of type list
	Else
		Local $aList[1][$nColumns]
		$aList[0][0] = "{BFE065C9-D90B-49FF-9600-D9E7B2811E93}"		; globally unique identifier of array of type list
	EndIf
	Return $aList
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListIsList
; Description....: Tests if given array is a list
; Syntax.........: _ListIsList($aList)
; Parameters.....: $aList					- List array as handle
; Return values..: True = A list, False = Not a list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListIsList(ByRef $aList)
	If Not IsArray($aList) Or UBound($aList) = 0 Then Return False
	If (UBound($aList,$UBOUND_DIMENSIONS) = 1 And $aList[0] <> "{BFE065C9-D90B-49FF-9600-D9E7B2811E93}") Or (UBound($aList,$UBOUND_DIMENSIONS) = 2 And $aList[0][0] <> "{BFE065C9-D90B-49FF-9600-D9E7B2811E93}") Then Return False
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListIsEmpty
; Description....: Tests if list is empty
; Syntax.........: _ListIsEmpty($aList)
; Parameters.....: $aList					- List array as handle
; Return values..: True = Empty, False = List contains elements
; Error values...: @error = 1				- Not a list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListIsEmpty(ByRef $aList)
	If Not _ListIsList($aList) Then Return SetError(1,0,True)
	If UBound($aList) = 1 Then Return True
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListClear
; Description....: Clears a list
; Syntax.........: _ListClear($aList)
; Parameters.....: $aList					- List array as handle
; Return values..: True = List cleared (emptied), False = Not a list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListClear(ByRef $aList)
	If Not _ListIsList($aList) Then Return False
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		ReDim $aList[1]
	Else
		ReDim $aList[1][UBound($aList,$UBOUND_COLUMNS)]
	EndIf
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListAdd
; Description....: Adds an element (row) to a list
; Syntax.........: _ListAdd($aList [,$vColumn1 = 0 [,$vColumn2 = 0 [,$vColumn3 = 0 [,$vColumn4 = 0 [,$vColumn5 = 0 [,$vColumn6 = 0 [,$vColumn7 = 0 [,$vColumn8 = 0 [,$vColumn9 = 0 [,$vColumn10 = 0 [,$vColumn11 = 0 [,$vColumn12 = 0 [,$vColumn13 = 0 [,$vColumn14 = 0 [,$vColumn15 = 0 [,$vColumn16 = 0 [,$vColumn17 = 0 [,$vColumn18 = 0 [,$vColumn19 = 0 [,$vColumn20 = 0]]]]]]]]]]]]]]]]]]]])
; Parameters.....: $aList					- List array as handle
;                  $vColumn1 to $vColumn20	- Value of column 1 to 20
; Return values..: True = Element added, False = Not a list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListAdd(ByRef $aList,$vColumn1 = 0,$vColumn2 = 0,$vColumn3 = 0,$vColumn4 = 0,$vColumn5 = 0,$vColumn6 = 0,$vColumn7 = 0,$vColumn8 = 0,$vColumn9 = 0,$vColumn10 = 0,$vColumn11 = 0,$vColumn12 = 0,$vColumn13 = 0,$vColumn14 = 0,$vColumn15 = 0,$vColumn16 = 0,$vColumn17 = 0,$vColumn18 = 0,$vColumn19 = 0,$vColumn20 = 0)
	If Not _ListIsList($aList) Then Return False
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		ReDim $aList[UBound($aList)+1]
		$aList[UBound($aList)-1] = $vColumn1
	Else
		ReDim $aList[UBound($aList)+1][UBound($aList,$UBOUND_COLUMNS)]
		__ListSetColumns($aList,UBound($aList)-1,$vColumn1,$vColumn2,$vColumn3,$vColumn4,$vColumn5,$vColumn6,$vColumn7,$vColumn8,$vColumn9,$vColumn10,$vColumn11,$vColumn12,$vColumn13,$vColumn14,$vColumn15,$vColumn16,$vColumn17,$vColumn18,$vColumn19,$vColumn20)
	EndIf
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListInsert
; Description....: Inserts an element (row) at given position into a list
; Syntax.........: _ListInsert($aList [,$nPosition = 0, [,$vColumn1 = 0 [,$vColumn2 = 0 [,$vColumn3 = 0 [,$vColumn4 = 0 [,$vColumn5 = 0 [,$vColumn6 = 0 [,$vColumn7 = 0 [,$vColumn8 = 0 [,$vColumn9 = 0 [,$vColumn10 = 0 [,$vColumn11 = 0 [,$vColumn12 = 0 [,$vColumn13 = 0 [,$vColumn14 = 0 [,$vColumn15 = 0 [,$vColumn16 = 0 [,$vColumn17 = 0 [,$vColumn18 = 0 [,$vColumn19 = 0 [,$vColumn20 = 0]]]]]]]]]]]]]]]]]]]]])
; Parameters.....: $aList					- List array as handle
;                  $nPosition				- Insert position in the list
;                  $vColumn1 to $vColumn20	- Value of column 1 to 20
; Return values..: True = Element inserted, False = Not a list or given position larger than list size
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListInsert(ByRef $aList,$nPosition = 0,$vColumn1 = 0,$vColumn2 = 0,$vColumn3 = 0,$vColumn4 = 0,$vColumn5 = 0,$vColumn6 = 0,$vColumn7 = 0,$vColumn8 = 0,$vColumn9 = 0,$vColumn10 = 0,$vColumn11 = 0,$vColumn12 = 0,$vColumn13 = 0,$vColumn14 = 0,$vColumn15 = 0,$vColumn16 = 0,$vColumn17 = 0,$vColumn18 = 0,$vColumn19 = 0,$vColumn20 = 0)
	If Not _ListIsList($aList) Or $nPosition+1 > UBound($aList) Then Return False
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		ReDim $aList[UBound($aList)+1]
		If $nPosition+2 < UBound($aList) Then
			For $nElement = UBound($aList)-1 To $nPosition+2 Step -1
				$aList[$nElement] = $aList[$nElement-1]
			Next
		EndIf
		$aList[$nPosition+1] = $vColumn1
	Else
		ReDim $aList[UBound($aList)+1][UBound($aList,$UBOUND_COLUMNS)]
		If $nPosition+2 < UBound($aList) Then
			For $nElement = UBound($aList)-1 To $nPosition+2 Step -1
				For $nColumn = 0 To UBound($aList,$UBOUND_COLUMNS)-1
					$aList[$nElement][$nColumn] = $aList[$nElement-1][$nColumn]
				Next
			Next
		EndIf
		__ListSetColumns($aList,$nPosition+1,$vColumn1,$vColumn2,$vColumn3,$vColumn4,$vColumn5,$vColumn6,$vColumn7,$vColumn8,$vColumn9,$vColumn10,$vColumn11,$vColumn12,$vColumn13,$vColumn14,$vColumn15,$vColumn16,$vColumn17,$vColumn18,$vColumn19,$vColumn20)
	EndIf
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListSet
; Description....: Sets columns of an element of a list
; Syntax.........: _ListSet($aList [,$nPosition = 0, [,$vColumn1 = 0 [,$vColumn2 = 0 [,$vColumn3 = 0 [,$vColumn4 = 0 [,$vColumn5 = 0 [,$vColumn6 = 0 [,$vColumn7 = 0 [,$vColumn8 = 0 [,$vColumn9 = 0 [,$vColumn10 = 0 [,$vColumn11 = 0 [,$vColumn12 = 0 [,$vColumn13 = 0 [,$vColumn14 = 0 [,$vColumn15 = 0 [,$vColumn16 = 0 [,$vColumn17 = 0 [,$vColumn18 = 0 [,$vColumn19 = 0 [,$vColumn20 = 0]]]]]]]]]]]]]]]]]]]]])
; Parameters.....: $aList					- List array as handle
;                  $nPosition				- Position in the list
;                  $vColumn1 to $vColumn20	- Value of column 1 to 20
; Return values..: True = Element set, False = Not a list or given position larger than list size
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListSet(ByRef $aList,$nPosition = 0,$vColumn1 = 0,$vColumn2 = 0,$vColumn3 = 0,$vColumn4 = 0,$vColumn5 = 0,$vColumn6 = 0,$vColumn7 = 0,$vColumn8 = 0,$vColumn9 = 0,$vColumn10 = 0,$vColumn11 = 0,$vColumn12 = 0,$vColumn13 = 0,$vColumn14 = 0,$vColumn15 = 0,$vColumn16 = 0,$vColumn17 = 0,$vColumn18 = 0,$vColumn19 = 0,$vColumn20 = 0)
	If Not _ListIsList($aList) Or $nPosition+2 > UBound($aList) Then Return False
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		$aList[$nPosition+1] = $vColumn1
	Else
		__ListSetColumns($aList,$nPosition+1,$vColumn1,$vColumn2,$vColumn3,$vColumn4,$vColumn5,$vColumn6,$vColumn7,$vColumn8,$vColumn9,$vColumn10,$vColumn11,$vColumn12,$vColumn13,$vColumn14,$vColumn15,$vColumn16,$vColumn17,$vColumn18,$vColumn19,$vColumn20)
	EndIf
	Return True
EndFunc

; #INTERNAL_USE_ONLY# ====================================================================================================================
; Name...........: __ListSetColumns
; Description....: Internal function to set columns of a 2D list
; Parameters.....: $aList					- List array as handle
;                  $nPosition				- Position in the list
;                  $vColumn1 to $vColumn20	- Value of column 1 to 20
; Return values..: None
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func __ListSetColumns(ByRef $aList,$nPosition,$vColumn1,$vColumn2,$vColumn3,$vColumn4,$vColumn5,$vColumn6,$vColumn7,$vColumn8,$vColumn9,$vColumn10,$vColumn11,$vColumn12,$vColumn13,$vColumn14,$vColumn15,$vColumn16,$vColumn17,$vColumn18,$vColumn19,$vColumn20)
	$aList[$nPosition][0] = $vColumn1
	If UBound($aList,$UBOUND_COLUMNS) > 1 Then $aList[$nPosition][1] = $vColumn2
	If UBound($aList,$UBOUND_COLUMNS) > 2 Then $aList[$nPosition][2] = $vColumn3
	If UBound($aList,$UBOUND_COLUMNS) > 3 Then $aList[$nPosition][3] = $vColumn4
	If UBound($aList,$UBOUND_COLUMNS) > 4 Then $aList[$nPosition][4] = $vColumn5
	If UBound($aList,$UBOUND_COLUMNS) > 5 Then $aList[$nPosition][5] = $vColumn6
	If UBound($aList,$UBOUND_COLUMNS) > 6 Then $aList[$nPosition][6] = $vColumn7
	If UBound($aList,$UBOUND_COLUMNS) > 7 Then $aList[$nPosition][7] = $vColumn8
	If UBound($aList,$UBOUND_COLUMNS) > 8 Then $aList[$nPosition][8] = $vColumn9
	If UBound($aList,$UBOUND_COLUMNS) > 9 Then $aList[$nPosition][9] = $vColumn10
	If UBound($aList,$UBOUND_COLUMNS) > 10 Then $aList[$nPosition][10] = $vColumn11
	If UBound($aList,$UBOUND_COLUMNS) > 11 Then $aList[$nPosition][11] = $vColumn12
	If UBound($aList,$UBOUND_COLUMNS) > 12 Then $aList[$nPosition][12] = $vColumn13
	If UBound($aList,$UBOUND_COLUMNS) > 13 Then $aList[$nPosition][13] = $vColumn14
	If UBound($aList,$UBOUND_COLUMNS) > 14 Then $aList[$nPosition][14] = $vColumn15
	If UBound($aList,$UBOUND_COLUMNS) > 15 Then $aList[$nPosition][15] = $vColumn16
	If UBound($aList,$UBOUND_COLUMNS) > 16 Then $aList[$nPosition][16] = $vColumn17
	If UBound($aList,$UBOUND_COLUMNS) > 17 Then $aList[$nPosition][17] = $vColumn18
	If UBound($aList,$UBOUND_COLUMNS) > 18 Then $aList[$nPosition][18] = $vColumn19
	If UBound($aList,$UBOUND_COLUMNS) > 19 Then $aList[$nPosition][19] = $vColumn20
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListRemove
; Description....: Removes an element from the list at given position
; Syntax.........: _ListRemove($aList [,$nPosition = 0])
; Parameters.....: $aList					- List array as handle
;                  $nPosition				- Position in the list
; Return values..: True = Element removed, False = Not a list or given position larger than list size
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListRemove(ByRef $aList, $nPosition = 0)
	If Not _ListIsList($aList) Or $nPosition+2 > UBound($aList) Then Return False
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		If $nPosition+1 < UBound($aList) Then
			For $nElement = $nPosition+1 To UBound($aList)-2
				$aList[$nElement] = $aList[$nElement+1]
			Next
		EndIf
		ReDim $aList[UBound($aList)-1]
	Else
		If $nPosition+1 < UBound($aList) Then
			For $nElement = $nPosition+1 To UBound($aList)-2
				For $nColumn = 0 To UBound($aList,$UBOUND_COLUMNS)-1
					$aList[$nElement][$nColumn] = $aList[$nElement+1][$nColumn]
				Next
			Next
		EndIf
		ReDim $aList[UBound($aList)-1][UBound($aList,$UBOUND_COLUMNS)]
	EndIf
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListRemoveByValue
; Description....: Removes an element from the list of given value, value can be any column of the list
; Syntax.........: _ListRemoveByValue($aList [,$vValue = 0])
; Parameters.....: $aList					- List array as handle
;                  $vValue					- Value to look for
; Return values..: True = Element removed, False = Not a list or value not in the list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListRemoveByValue(ByRef $aList, $vValue = 0)
	Local $nPosition = _ListValueInList($aList, $vValue)
	If $nPosition > -1 Then
		_listRemove($aList, $nPosition)
		Return True
	EndIf
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListRemoveByColumnValue
; Description....: Removes an element from the list of given value in given column
; Syntax.........: _ListRemoveByValue($aList [,$vValue = 0])
; Parameters.....: $aList					- List array as handle
;                  $vValue					- Value to look for
;                  $nColumn					- Column number when 2D list
; Return values..: True = Element removed, False = Not a list or value not in the column of the list or given column to large
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListRemoveByColumnValue(ByRef $aList, $vValue = 0, $nColumn = 0)
	If $nColumn > UBound($aList,$UBOUND_COLUMNS)-1 Then Return False
	Local $nPosition = _ListValueInListColumn($aList, $vValue, $nColumn)
	If $nPosition > -1 Then
		_listRemove($aList, $nPosition)
		Return True
	EndIf
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListGet
; Description....: Gets (array) value of given position of a list
; Syntax.........: _ListGet($aList [,$nPosition = 0])
; Parameters.....: $aList					- List array as handle
;                  $nPosition				- Position in the list
; Return values..: (Array) value of given position, 1D = value, 2D = Array
; Error values...: @error = 1				- Not a list
;                  @error = 2				- List is empty, no values
;                  @error = 3				- Given position greater than list size
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListGet(ByRef $aList,$nPosition = 0)
	If Not _ListIsList($aList) Then Return SetError(1,0,0)
	If _ListIsEmpty($aList) Then Return SetError(2,0,0)
	If $nPosition+2 > UBound($aList) Then Return SetError(3,0,0)
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		Return $aList[$nPosition+1]
	Else
		Local $aElement[UBound($aList,$UBOUND_COLUMNS)]
		For $nColumn = 0 To UBound($aList,$UBOUND_COLUMNS)-1
			$aElement[$nColumn] = $aList[$nPosition+1][$nColumn]
		Next
		Return $aElement
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListGetColumn
; Description....: Gets (column) value of given position of a list
; Syntax.........: _ListGetColumn($aList [,$nPosition = 0 [,$Column = 0]])
; Parameters.....: $aList					- List array as handle
;                  $nPosition				- Position in the list
;                  $nColumn					- Column number if 2D list
; Return values..: (Column) value of given position, 1D = value (no column number needed), 2D = column value
; Error values...: @error = 1				- Not a list
;                  @error = 2				- List is empty, no values
;                  @error = 3				- Given position greater than list size
;                  @error = 4				- Given column greater than number of columns
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListGetColumn(ByRef $aList,$nPosition = 0,$nColumn = 0)
	If Not _ListIsList($aList) Then Return SetError(1,0,0)
	If _ListIsEmpty($aList) Then Return SetError(2,0,0)
	If $nPosition+2 > UBound($aList) Then Return SetError(3,0,0)
	If $nColumn > UBound($aList,$UBOUND_COLUMNS)-1 Then Return SetError(4,0,0)
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		Return $aList[$nPosition+1]
	Else
		Return $aList[$nPosition+1][$nColumn]
	EndIf
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListValueInList
; Description....: Tests if a value is any column of the list
; Syntax.........: _ListValueInList($aList [, $vValue [, $bCaseSensitive]])
; Parameters.....: $aList					- List array as handle
;                  $vValue					- Value to test
;                  $bCaseSensitive			- True = case sensitive for strings, False = case insensitive
; Return values..: > -1 position in the list, -1 = not in the list or not a list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListValueInList(ByRef $aList,$vValue = 0,$bCaseSensitive = False)
	If Not _ListIsList($aList) Then Return -1
	If UBound($aList) = 1 Then Return -1
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		For $nList = 1 To UBound($aList)-1
			If $bCaseSensitive Then
				If VarGetType($aList[$nList]) = VarGetType($vValue) And $aList[$nList] == $vValue Then Return $nList-1
			Else
				If VarGetType($aList[$nList]) = VarGetType($vValue) And $aList[$nList] = $vValue Then Return $nList-1
			EndIf
		Next
		Else
		For $nList = 1 To UBound($aList)-1
			For $nColumn = 0 To UBound($aList,$UBOUND_COLUMNS)-1
				If $bCaseSensitive Then
					If VarGetType($aList[$nList][$nColumn]) = VarGetType($vValue) And $aList[$nList][$nColumn] == $vValue Then
						SetExtended($nColumn)
						Return $nList-1
					EndIf
				Else
					If VarGetType($aList[$nList][$nColumn]) = VarGetType($vValue) And $aList[$nList][$nColumn] = $vValue Then
						SetExtended($nColumn)
						Return $nList-1
					EndIf
				EndIf
			Next
		Next
	EndIf
	Return -1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListValueInListColumn
; Description....: Tests if a value is in given column of the list
; Syntax.........: _ListValueInListColumn($aList [, $vValue [,$nColumn [, $bCaseSensitive]]])
; Parameters.....: $aList					- List array as handle
;                  $nColumn					- Column number when 2D list
;                  $vValue					- Value to test
;                  $bCaseSensitive			- True = case sensitive for strings, False = case insensitive
; Return values..: > -1 position in the list, -1 = not in the list or not a list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListValueInListColumn(ByRef $aList,$vValue = 0,$nColumn = 0,$bCaseSensitive = False)
	If Not _ListIsList($aList) Then Return -1
	If UBound($aList) = 1 Then Return -1
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		For $nList = 1 To UBound($aList)-1
			If $bCaseSensitive Then
				If VarGetType($aList[$nList]) = VarGetType($vValue) And $aList[$nList] == $vValue Then Return $nList-1
			Else
				If VarGetType($aList[$nList]) = VarGetType($vValue) And $aList[$nList] = $vValue Then Return $nList-1
			EndIf
		Next
		Else
		For $nList = 1 To UBound($aList)-1
			If $bCaseSensitive Then
				If VarGetType($aList[$nList][$nColumn]) = VarGetType($vValue) And $aList[$nList][$nColumn] == $vValue Then
					SetExtended($nColumn)
					Return $nList-1
				EndIf
			Else
				If VarGetType($aList[$nList][$nColumn]) = VarGetType($vValue) And $aList[$nList][$nColumn] = $vValue Then
					SetExtended($nColumn)
					Return $nList-1
				EndIf
			EndIf
		Next
	EndIf
	Return -1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListSize
; Description....: Gets list size
; Syntax.........: _ListSize($aList)
; Parameters.....: $aList					- List array as handle
; Return values..: Size of list
; Error values...: @error = 1				- Not a list
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListSize(ByRef $aList)
	If Not _ListIsList($aList) Then Return False
	Return UBound($aList)-1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ListToArray
; Description....: Creates an array from the given list
; Syntax.........: _ListToArray($aList)
; Parameters.....: $aList					- List array as handle
; Return values..: Array of all list values
; Error values...: @error = 1				- Not a list
;                  @error = 2				- List is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ListToArray(ByRef $aList)
	If Not _ListIsList($aList) Then Return SetError(1,0,0)
	If UBound($aList) = 1 Then Return SetError(2,0,0)
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		Local $aArray[UBound($aList)-1]
		For $nPosition = 1 To UBound($aList)-1
			$aArray[$nPosition-1] = $aList[$nPosition]
		Next
	Else
		Local $aArray[UBound($aList)-1][UBound($aList,$UBOUND_COLUMNS)]
		For $nPosition = 1 To UBound($aList)-1
			For $nColumn = 0 To UBound($aList,$UBOUND_COLUMNS)-1
				$aArray[$nPosition-1][$nColumn] = $aList[$nPosition][$nColumn]
			Next
		Next
	EndIf
	Return $aArray
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ArrayToList
; Description....: Creates a list from the given array
; Syntax.........: _ArrayToList($aArray)
; Parameters.....: $aArray					- Array to transfer to a list
; Return values..: List array as handle
; Error values...: @error = 1				- Parameter not an array
;                  @error = 2				- Array is empty
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ArrayToList(ByRef $aArray)
	If Not IsArray($aArray) Then SetError(1,0,0)
	If UBound($aArray) = 0 Then SetError(2,0,0)
	Local $aList = _ListCreate(UBound($aArray,$UBOUND_COLUMNS))
	If UBound($aList,$UBOUND_DIMENSIONS) = 1 Then
		ReDim $aList[UBound($aArray)+1]
		For $nArray = 0 To UBound($aArray)-1
			$aList[$nArray+1] = $aArray[$nArray]
		Next
	Else
		ReDim $aList[UBound($aArray)+1][UBound($aArray,$UBOUND_COLUMNS)]
		For $nArray = 0 To UBound($aArray)-1
			For $nColumn = 0 To UBound($aArray,$UBOUND_COLUMNS)-1
				$aList[$nArray+1][$nColumn] = $aArray[$nArray][$nColumn]
			Next
		Next
	EndIf
	Return $aList
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackCreate
; Description....: Creates a stack (LIFO) for pushing and popping values
; Syntax.........: _StackCreate()
; Parameters.....: None
; Return values..: Stack array as handle
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackCreate()
	Local $aStack = ["{16AAC206-5CBD-4623-B5A2-3A81B85B1ED3}"]	; globally unique identifier of array of type stack
	Return $aStack
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackIsStack
; Description....: Tests if given array is a stack
; Syntax.........: _StackIsStack($aStack)
; Parameters.....: $aStack					- Stack array as handle
; Return values..: True = A stack, False = Not a stack
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackIsStack(ByRef $aStack)
	If Not IsArray($aStack) Or UBound($aStack) = 0 Or $aStack[0] <> "{16AAC206-5CBD-4623-B5A2-3A81B85B1ED3}" Then Return False
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackIsEmpty
; Description....: Tests if stack is empty
; Syntax.........: _StackIsEmpty($aStack)
; Parameters.....: $aStack					- Stack array as handle
; Return values..: True = Empty, False = Stack contains elements
; Error values...: @error = 1				- Not a stack
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackIsEmpty(ByRef $aStack)
	If Not _StackIsStack($aStack) Then Return SetError(1,0,True)
	If UBound($aStack) = 1 Then Return True
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackClear
; Description....: Clears a stack
; Syntax.........: _StackClear($aStack)
; Parameters.....: $aStack					- Stack array as handle
; Return values..: True = Stack cleared (emptied), False = Not a stack
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackClear(ByRef $aStack)
	If Not _StackIsStack($aStack) Then Return False
	ReDim $aStack[1]
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackPush
; Description....: Pushes (adds) a value onto the given stack at last position
; Syntax.........: _StackPush($aStack [, $vValue = 0])
; Parameters.....: $aStack					- Stack array as handle
;                  $vValue					- Value to push onto stack
; Return values..: True = Value pushed, False = Not a stack
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackPush(ByRef $aStack,$vValue = 0)
	If Not _StackIsStack($aStack) Then Return False
	ReDim $aStack[UBound($aStack)+1]
	$aStack[UBound($aStack)-1] = $vValue
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackPop
; Description....: Pops (removes) a value from the given stack at last position
; Syntax.........: _StackPop($aStack)
; Parameters.....: $aStack					- Stack array as handle
; Return values..: Value popped from stack
; Error values...: @error = 1				- Not a stack
;                  @error = 2				- Stack is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackPop(ByRef $aStack)
	If Not _StackIsStack($aStack) Then Return SetError(1,0,0)
	If UBound($aStack) = 1 Then Return SetError(2,0,0)
	Local $vValue = $aStack[UBound($aStack)-1]
	ReDim $aStack[UBound($aStack)-1]
	Return $vValue
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackPeek
; Description....: Gets the last pushed value without popping (removing)
; Syntax.........: _StackPeek($aStack)
; Parameters.....: $aStack					- Stack array as handle
; Return values..: Last value pushed onto stack
; Error values...: @error = 1				- Not a stack
;                  @error = 2				- Stack is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackPeek(ByRef $aStack)
	If Not _StackIsStack($aStack) Then Return SetError(1,0,0)
	If UBound($aStack) = 1 Then Return SetError(2,0,0)
	Return $aStack[UBound($aStack)-1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackGet
; Description....: Gets a value of a given position, zero based
; Syntax.........: _StackGet($aStack [,$nPosition = 0])
; Parameters.....: $aStack					- Stack array as handle
;                  $nPosition				- Position in stack
; Return values..: Value of given position
; Error values...: @error = 1				- Not a stack
;                  @error = 2				- Stack is empty, no values
;                  @error = 3				- Given position greater than stack size
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackGet(ByRef $aStack,$nPosition = 0)
	If Not _StackIsStack($aStack) Then Return SetError(1,0,0)
	If UBound($aStack) = 1 Then Return SetError(2,0,0)
	If $nPosition > UBound($aStack)-2 Then Return SetError(3,0,0)
	Return $aStack[$nPosition+1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackValueInStack
; Description....: Tests if a value is in the stack
; Syntax.........: _StackValueInStack($aStack [,$vValue = 0 [,$bCaseSensitive]])
; Parameters.....: $aStack					- Stack array as handle
;                  $vValue					- Value to test
;                  $bCaseSensitive			- True = case sensitive for strings, False = case insensitive
; Return values..: > -1 position in stack, -1 = not in stack or not a stack
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _StackValueInStack(ByRef $aStack,$vValue = 0,$bCaseSensitive = False)
	If Not _StackIsStack($aStack) Then Return -1
	If UBound($aStack) = 1 Then Return -1
	For $nStack = 1 To UBound($aStack)-1
		If $bCaseSensitive Then
			If VarGetType($aStack[$nStack]) = VarGetType($vValue) And $aStack[$nStack] == $vValue Then Return $nStack-1
		Else
			If VarGetType($aStack[$nStack]) = VarGetType($vValue) And $aStack[$nStack] = $vValue Then Return $nStack-1
		EndIf
	Next
	Return -1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackSize
; Description....: Gets stack size
; Syntax.........: _StackSize($aStack)
; Parameters.....: $aStack					- Stack array as handle
; Return values..: Size of stack
; Error values...: @error = 1				- Not a stack
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackSize(ByRef $aStack)
	If Not _StackIsStack($aStack) Then Return SetError(1,0,0)
	Return UBound($aStack)-1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _StackToArray
; Description....: Creates an array from the given stack
; Syntax.........: _StackToArray($aStack)
; Parameters.....: $aStack					- Stack array as handle
; Return values..: Array of all stack values
; Error values...: @error = 1				- Not a stack
;                  @error = 2				- Stack is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _StackToArray(ByRef $aStack)
	If Not _StackIsStack($aStack) Then Return SetError(1,0,0)
	If UBound($aStack) = 1 Then Return SetError(2,0,0)
	Local $aArray[UBound($aStack)-1]
	For $nStack = 1 To UBound($aStack)-1
		$aArray[$nStack-1] = $aStack[$nStack]
	Next
	Return $aArray
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ArrayToStack
; Description....: Creates a stack from the given array
; Syntax.........: _ArrayToStack($aArray)
; Parameters.....: $aArray					- Array to transfer to stack
; Return values..: Stack array as handle
; Error values...: @error = 1				- Parameter not an array
;                  @error = 2				- Array is empty
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ArrayToStack(ByRef $aArray)
	If Not IsArray($aArray) Then SetError(1,0,0)
	If UBound($aArray) = 0 Then SetError(2,0,0)
	Local $aStack = _StackCreate()
	ReDim $aStack[UBound($aArray)+1]
	For $nArray = 0 To UBound($aArray)-1
		$aStack[$nArray+1] = $aArray[$nArray]
	Next
	Return $aStack
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftCreate
; Description....: Creates a shift register (FIFO) for pushing and popping values
; Parameters.....: None
; Syntax.........: _ShiftCreate()
; Return values..: Shift array as handle
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftCreate()
	Local $aShift = ["{9C3325C5-A6FE-4175-AFB5-84C41862D58C}"]	; globally unique identifier of array of type shift register
	Return $aShift
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftIsShift
; Description....: Tests if given array is a shift register
; Syntax.........: _ShiftIsShift($aShift)
; Parameters.....: $aShift					- Shift register array as handle
; Return values..: True = A shift register, False = Not a shift register
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftIsShift(ByRef $aShift)
	If Not IsArray($aShift) Or UBound($aShift) = 0 Or $aShift[0] <> "{9C3325C5-A6FE-4175-AFB5-84C41862D58C}" Then Return False
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftIsEmpty
; Description....: Tests if shift register is empty
; Syntax.........: _ShiftIsEmpty($aShift)
; Parameters.....: $aShift					- Shift register array as handle
; Return values..: True = Empty, False = Shift register contains elements
; Error values...: @error = 1				- Not a shift register
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftIsEmpty(ByRef $aShift)
	If Not _ShiftIsShift($aShift) Then Return SetError(1,0,True)
	If UBound($aShift) = 1 Then Return True
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftClear
; Description....: Clears a shift register
; Syntax.........: _ShiftClear($aShift)
; Parameters.....: $aShift					- Shift register array as handle
; Return values..: True = Shift register cleared (emptied), False = Not a shift register
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftClear(ByRef $aShift)
	If Not _ShiftIsShift($aShift) Then Return False
	ReDim $aShift[1]
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftPush
; Description....: Pushes (adds) a value into the given shift register on first position
; Syntax.........: _ShiftPush($aShift [,$vValue = 0])
; Parameters.....: $aShift					- Shift register array as handle
;                  $vValue					- Value to push into shift register
; Return values..: True = Value pushed, False = Not a shift register
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftPush(ByRef $aShift,$vValue = 0)
	If Not _ShiftIsShift($aShift) Then Return False
	ReDim $aShift[UBound($aShift)+1]
	For $nShift = UBound($aShift)-2 To 1 Step -1
		$aShift[$nShift+1] = $aShift[$nShift]
	Next
	$aShift[1] = $vValue
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftPop
; Description....: Pops (removes) a value from the given shift register at last position
; Syntax.........: _ShiftPop($aShift)
; Parameters.....: $aShift					- Shift register array as handle
; Return values..: Value popped from shift register
; Error values...: @error = 1				- Not a shift register
;                  @error = 2				- Shift register is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftPop(ByRef $aShift)
	If Not _ShiftIsShift($aShift) Then Return SetError(1,0,0)
	If UBound($aShift) = 1 Then Return SetError(2,0,0)
	Local $vValue = $aShift[UBound($aShift)-1]
	ReDim $aShift[UBound($aShift)-1]
	Return $vValue
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftPeek
; Description....: Gets the value to pop without actual popping (removing)
; Syntax.........: _ShiftPeek($aShift)
; Parameters.....: $aShift					- Shift register array as handle
; Return values..: Value to pop from shift register
; Error values...: @error = 1				- Not a shift register
;                  @error = 2				- Shift register is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftPeek(ByRef $aShift)
	If Not _ShiftIsShift($aShift) Then Return SetError(1,0,0)
	If UBound($aShift) = 1 Then Return SetError(2,0,0)
	Return $aShift[UBound($aShift)-1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftGet
; Description....: Gets a value of a given position, zero based
; Syntax.........: _ShiftGet($aShift [,$nPosition = 0])
; Parameters.....: $aShift					- Shift register array as handle
;                  $nPosition				- Position in shift register
; Return values..: Value of given position
; Error values...: @error = 1				- Not a shift register
;                  @error = 2				- Shift register is empty, no values
;                  @error = 3				- Given position greater than shift register size
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftGet(ByRef $aShift,$nPosition = 0)
	If Not _ShiftIsShift($aShift) Then Return SetError(1,0,0)
	If UBound($aShift) = 1 Then Return SetError(2,0,0)
	If $nPosition > UBound($aShift)-2 Then Return SetError(3,0,0)
	Return $aShift[$nPosition+1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftValueInShift
; Description....: Tests if a value is in the shift register
; Syntax.........: _ShiftValueInShift($aShift [,$vValue [,$bCaseSensitive]])
; Parameters.....: $aShift					- Shift register array as handle
;                  $vValue					- Value to test
;                  $bCaseSensitive			- True = case sensitive for strings, False = case insensitive
; Return values..: > -1 position in shift register, -1 = not in shift register or not a shift register
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _ShiftValueInShift(ByRef $aShift,$vValue = 0,$bCaseSensitive = False)
	If Not _ShiftIsShift($aShift) Then Return -1
	If UBound($aShift) = 1 Then Return -1
	For $nShift = 1 To UBound($aShift)-1
		If $bCaseSensitive Then
			If VarGetType($aShift[$nShift]) = VarGetType($vValue) And $aShift[$nShift] == $vValue Then Return $nShift-1
		Else
			If VarGetType($aShift[$nShift]) = VarGetType($vValue) And $aShift[$nShift] = $vValue Then Return $nShift-1
		EndIf
	Next
	Return -1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftSize
; Description....: Gets shift register size
; Syntax.........: _ShiftSize($aShift)
; Parameters.....: $aShift					- Shift register array as handle
; Return values..: Size of shift register
; Error values...: @error = 1				- Not a shift register
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftSize(ByRef $aShift)
	If Not _ShiftIsShift($aShift) Then Return SetError(1,0,0)
	Return UBound($aShift)-1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ShiftToArray
; Description....: Creates an array from the given shift register
; Syntax.........: _ShiftToArray($aShift)
; Parameters.....: $aShift					- Shift register array as handle
; Return values..: Array of all shift register values
; Error values...: @error = 1				- Not a shift register
;                  @error = 2				- Shift register is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ShiftToArray(ByRef $aShift)
	If Not _ShiftIsShift($aShift) Then Return SetError(1,0,0)
	If UBound($aShift) = 1 Then Return SetError(2,0,0)
	Local $aArray[UBound($aShift)-1]
	For $nShift = 1 To UBound($aShift)-1
		$aArray[$nShift-1] = $aShift[$nShift]
	Next
	Return $aArray
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ArrayToShift
; Description....: Creates a shift register from the given array
; Syntax.........: _ArrayToShift($aArray)
; Parameters.....: $aArray					- Array to transfer to shift register
; Return values..: Shift register array as handle
; Error values...: @error = 1				- Parameter not an array
;                  @error = 2				- Array is empty
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ArrayToShift(ByRef $aArray)
	If Not IsArray($aArray) Then SetError(1,0,0)
	If UBound($aArray) = 0 Then SetError(2,0,0)
	Local $aShift = _ShiftCreate()
	ReDim $aShift[UBound($aArray)+1]
	For $nArray = 0 To UBound($aArray)-1
		$aShift[$nArray+1] = $aArray[$nArray]
	Next
	Return $aShift
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapCreate
; Description....: Creates a key map for storing values attach to keys, can be use bidirectional
; Syntax.........: _MapCreate()
; Parameters.....: None
; Return values..: Key map array as handle
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapCreate()
	Local $aMap = [["{3B093053-E472-4E88-B216-47ADC7FB6A2F}",0]]	; globally unique identifier of array of type key map
	Return $aMap
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapIsMap
; Description....: Tests if given array is a key map
; Syntax.........: _MapIsMap($aMap)
; Parameters.....: $aMap					- Key map array as handle
; Return values..: True = A key map, False = Not a key map
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapIsMap(ByRef $aMap)
	If Not IsArray($aMap) Or UBound($aMap) = 0 Or $aMap[0][0] <> "{3B093053-E472-4E88-B216-47ADC7FB6A2F}" Then Return False
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapIsEmpty
; Description....: Tests if key map is empty
; Syntax.........: _MapIsEmpty($aMap)
; Parameters.....: $aMap					- Key map array as handle
; Return values..: True = Empty, False = Key map contains elements
; Error values...: @error = 1				- Not a key map
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapIsEmpty(ByRef $aMap)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,True)
	If UBound($aMap) = 1 Then Return True
	Return False
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapClear
; Description....: Clears a key map
; Syntax.........: _MapClear($aMap)
; Parameters.....: $aMap					- Key map array as handle
; Return values..: True = Key map cleared (emptied), False = Not a key map
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapClear(ByRef $aMap)
	If Not _MapIsMap($aMap) Then Return False
	ReDim $aMap[1][2]
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapAdd
; Description....: Adds a key-value pair to the given key map
; Syntax.........: _MapAdd($aMap, $vKey [,$vValue = 0])
; Parameters.....: $aMap					- Key map array as handle
;                  $vKey					- Key to identify value
;                  $vValue					- Value to add
; Return values..: True = Key-value pair added, False = Not a key map
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapAdd(ByRef $aMap,$vKey,$vValue = 0)
	If Not _MapIsMap($aMap) Then Return False
	ReDim $aMap[UBound($aMap)+1][2]
	$aMap[UBound($aMap)-1][0] = $vKey
	$aMap[UBound($aMap)-1][1] = $vValue
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapValueByKey
; Description....: Gets a value of given key
; Syntax.........: _MapValueByKey($aMap, $vKey [,$bCaseSensitive = False])
; Parameters.....: $aMap					- Key map array as handle
;                  $vKey					- Key to search for
;                  $bCaseSensitive 			- True = case sensitive for strings, False = case insensitive
; Return values..: Value belonging to key
; Error values...: @error = 1				- Not a key map
;                  @error = 2				- Key map is empty, no values
;                  @error = 3				- Key not found
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _MapValueByKey(ByRef $aMap,$vKey,$bCaseSensitive = False)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,0)
	If UBound($aMap) = 1 Then Return SetError(2,0,0)
	For $nMap = 1 To UBound($aMap)-1
		If $bCaseSensitive Then
			If VarGetType($aMap[$nMap][0]) = VarGetType($vKey) And $aMap[$nMap][0] == $vKey Then Return $aMap[$nMap][1]
		Else
			If VarGetType($aMap[$nMap][0]) = VarGetType($vKey) And $aMap[$nMap][0] = $vKey Then Return $aMap[$nMap][1]
		EndIf
	Next
	Return SetError(3,0,0)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapKeyByValue
; Description....: Gets a key of given value
; Syntax.........: _MapKeyByValue($aMap, $vValue [,$bCaseSensitive = False])
; Parameters.....: $aMap					- Key map array as handle
;                  $vValue					- Value to search for
;                  $bCaseSensitive 			- True = case sensitive for strings, False = case insensitive
; Return values..: Key belonging to value
; Error values...: @error = 1				- Not a key map
;                  @error = 2				- Key map is empty, no values
;                  @error = 3				- Value not found
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _MapKeyByValue(ByRef $aMap,$vValue,$bCaseSensitive = False)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,0)
	If UBound($aMap) = 1 Then Return SetError(2,0,0)
	For $nMap = 1 To UBound($aMap)-1
		If $bCaseSensitive Then
			If VarGetType($aMap[$nMap][1]) = VarGetType($vValue) And $aMap[$nMap][1] == $vValue Then Return $aMap[$nMap][0]
		Else
			If VarGetType($aMap[$nMap][1]) = VarGetType($vValue) And $aMap[$nMap][1] = $vValue Then Return $aMap[$nMap][0]
		EndIf
	Next
	Return SetError(3,0,0)
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapRemoveByKey
; Description....: Removes a key-value pair by key from the given key map
; Syntax.........: _MapRemoveByKey($aMap, $vKey [,$bCaseSensitive = False])
; Parameters.....: $aMap					- Key map array as handle
;                  $vKey					- Key to use
;                  $bCaseSensitive			- True = case sensitive for strings, False = case insensitive
; Return values..: True = Key-value pair removed, False = not removed
; Error values...: @error = 1				- Not a key map
;                  @error = 2				- Key map is empty, no values
;                  @error = 3				- Key not found
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _MapRemoveByKey(ByRef $aMap,$vKey,$bCaseSensitive = False)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,False)
	If UBound($aMap) = 1 Then Return SetError(2,0,False)
	Local $nPosition = 0
	For $nMap = 1 To UBound($aMap)-1
		If $bCaseSensitive Then
			If VarGetType($aMap[$nMap][0]) = VarGetType($vKey) And $aMap[$nMap][0] == $vKey Then
				$nPosition = $nMap
				ExitLoop
			EndIf
		Else
			If VarGetType($aMap[$nMap][0]) = VarGetType($vKey) And $aMap[$nMap][0] = $vKey Then
				$nPosition = $nMap
				ExitLoop
			EndIf
		EndIf
	Next
	If $nPosition = 0 Then Return SetError(3,0,False)
	For $nMap = $nPosition To UBound($aMap)-2
		$aMap[$nMap][0] = $aMap[$nMap+1][0]
		$aMap[$nMap][1] = $aMap[$nMap+1][1]
	Next
	ReDim $aMap[UBound($aMap)-1][2]
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapRemoveByValue
; Description....: Removes a key-value pair by value from the given key map
; Syntax.........: _MapRemoveByValue($aMap, $vValue [,$bCaseSensitive = False])
; Parameters.....: $aMap					- Key map array as handle
;                  $vValue					- Value to use
;                  $bCaseSensitive			- True = case sensitive for strings, False = case insensitive
; Return values..: True = Key-value pair removed, False = not removed
; Error values...: @error = 1				- Not a key map
;                  @error = 2				- Key map is empty, no values
;                  @error = 3				- Key not found
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _MapRemoveByValue(ByRef $aMap,$vValue,$bCaseSensitive = False)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,False)
	If UBound($aMap) = 1 Then Return SetError(2,0,False)
	Local $nPosition = 0
	For $nMap = 1 To UBound($aMap)-1
		If $bCaseSensitive Then
			If VarGetType($aMap[$nMap][1]) = VarGetType($vValue) And $aMap[$nMap][1] == $vValue Then
				$nPosition = $nMap
				ExitLoop
			EndIf
		Else
			If VarGetType($aMap[$nMap][1]) = VarGetType($vValue) And $aMap[$nMap][1] = $vValue Then
				$nPosition = $nMap
				ExitLoop
			EndIf
		EndIf
	Next
	If $nPosition = 0 Then Return SetError(3,0,False)
	For $nMap = $nPosition To UBound($aMap)-2
		$aMap[$nMap][0] = $aMap[$nMap+1][0]
		$aMap[$nMap][1] = $aMap[$nMap+1][1]
	Next
	ReDim $aMap[UBound($aMap)-1][2]
	Return True
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapGetKey
; Description....: Gets a key of a given position, zero based
; Syntax.........: _MapGetKey($aMap [,$nPosition = 0])
; Parameters.....: $aMap					- Key map array as handle
;                  $nPosition				- Position in key map
; Return values..: Key of given position
; Error values...: @error = 1				- Not a key map
;                  @error = 2				- Key map is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapGetKey(ByRef $aMap,$nPosition = 0)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,0)
	If UBound($aMap) = 1 Then Return SetError(2,0,0)
	If $nPosition > UBound($aMap)-2 Then Return SetError(3,0,0)
	Return $aMap[$nPosition+1][0]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapGetValue
; Description....: Gets a value of a given position, zero based
; Syntax.........: _MapGetValue($aMap [,$nPosition = 0])
; Parameters.....: $aMap					- Key map array as handle
; Parameters.....: $nPosition				- Position in key map
; Return values..: Value of given position
; Error values...: @error = 1				- Not a key map
;                  @error = 2				- Key map is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapGetValue(ByRef $aMap,$nPosition = 0)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,0)
	If UBound($aMap) = 1 Then Return SetError(2,0,0)
	If $nPosition > UBound($aMap)-2 Then Return SetError(3,0,0)
	Return $aMap[$nPosition+1][1]
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapKeyInMap
; Description....: Tests if a key is in the key map
; Syntax.........: _MapKeyInMap($aMap, $vKey)
; Parameters.....: $aMap					- Key map array as handle
;                  $vKey					- Key to test
;                  $bCaseSensitive = False	- True = case sensitive for strings, False = case insensitive
; Return values..: > -1 position in key map, -1 = not in key map or not a key map
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _MapKeyInMap(ByRef $aMap,$vKey = 0,$bCaseSensitive = False)
	If Not _MapIsMap($aMap) Then Return -1
	If UBound($aMap) = 1 Then Return -1
	For $nMap = 1 To UBound($aMap)-1
		If $bCaseSensitive Then
			If VarGetType($aMap[$nMap][0]) = VarGetType($vKey) And $aMap[$nMap][0] == $vKey Then Return $nMap-1
		Else
			If VarGetType($aMap[$nMap][0]) = VarGetType($vKey) And $aMap[$nMap][0] = $vKey Then Return $nMap-1
		EndIf
	Next
	Return -1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapValueInMap
; Description....: Tests if a value is in the key map
; Syntax.........: _MapValueInMap($aMap, $vValue)
; Parameters.....: $aMap					- Key map array as handle
;                  $vValue					- Value to test
;                  $bCaseSensitive = False	- True = case sensitive for strings, False = case insensitive
; Return values..: > -1 position in key map, -1 = not in key map or not a key map
; Author.........: Peter Verbeek
; Modified.......: 1.01		VarGetType added to compare if variable types are the same
; ===============================================================================================================================
Func _MapValueInMap(ByRef $aMap,$vValue = 0,$bCaseSensitive = False)
	If Not _MapIsMap($aMap) Then Return -1
	If UBound($aMap) = 1 Then Return -1
	For $nMap = 1 To UBound($aMap)-1
		If $bCaseSensitive Then
			If VarGetType($aMap[$nMap][1]) = VarGetType($vValue) And $aMap[$nMap][1] == $vValue Then Return $nMap-1
		Else
			If VarGetType($aMap[$nMap][1]) = VarGetType($vValue) And $aMap[$nMap][1] = $vValue Then Return $nMap-1
		EndIf
	Next
	Return -1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapSize
; Description....: Gets key map size
; Syntax.........: _MapSize($aMap)
; Parameters.....: $aMap					- Key map array as handle
; Return values..: Size of key map
; Error values...: @error = 1				- Not a key map
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapSize(ByRef $aMap)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,0)
	Return UBound($aMap)-1
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _MapToArray
; Description....: Creates an array from the given key map
; Syntax.........: _MapToArray($aMap)
; Parameters.....: $aMap					- Key map array as handle
;                  $bValuesOnly = False		- True = Values, no keys, False = Values and keys (default)
; Return values..: Array of all key map (keys and) values
; Error values...: @error = 1				- Not a key map
;                  @error = 2				- Key map is empty, no values
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _MapToArray(ByRef $aMap,$bValuesOnly = False)
	If Not _MapIsMap($aMap) Then Return SetError(1,0,0)
	If UBound($aMap) = 1 Then Return SetError(2,0,0)
	If $bValuesOnly Then
		Local $aArray[UBound($aMap)-1]
		For $nMap = 1 To UBound($aMap)-1
			$aArray[$nMap-1] = $aMap[$nMap][1]
		Next
	Else
		Local $aArray[UBound($aMap)-1][2]
		For $nMap = 1 To UBound($aMap)-1
			$aArray[$nMap-1][0] = $aMap[$nMap][0]	; key
			$aArray[$nMap-1][1] = $aMap[$nMap][1]	; value
			ConsoleWrite($aArray[$nMap-1][0] & " " & $aArray[$nMap-1][1])
		Next
	EndIf
	Return $aArray
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: _ArrayToMap
; Description....: Creates a key map from the given 2D array
; Syntax.........: _ArrayToMap($aArray)
; Parameters.....: $aArray					- Array to transfer to key map
; Return values..: Key map array as handle
; Error values...: @error = 1				- Parameter not an array
;                  @error = 2				- Array is empty
;                  @error = 3				- Not a 2D array
; Author.........: Peter Verbeek
; Modified.......:
; ===============================================================================================================================
Func _ArrayToMap(ByRef $aArray)
	If Not IsArray($aArray) Then SetError(1,0,0)
	If UBound($aArray) = 0 Then SetError(2,0,0)
	If UBound($aArray,$UBOUND_DIMENSIONS) < 2 Then SetError(3,0,0)
	Local $aMap = _MapCreate()
	ReDim $aMap[UBound($aArray)+1][2]
	For $nArray = 0 To UBound($aArray)-1
		$aMap[$nArray+1][0] = $aArray[$nArray][0]
		$aMap[$nArray+1][1] = $aArray[$nArray][1]
	Next
	Return $aMap
EndFunc