# Orphaned REST Handler Methods - Complete Analysis

**Status**: 11 orphaned handler methods identified for deletion
**Generated**: November 23, 2025

---

## Executive Summary

After Batches 1-4 gRPC migrations, the following REST handler methods are now **orphaned** (no longer called from anywhere):
- **Total Orphaned**: 11 methods
- **Safe to Delete**: 10 methods
- **Requires Verification**: 1 method (CloneSectionContentLanguage)
- **Still In Use**: 1 method (RetrieveFilingData) - DO NOT DELETE

---

## 🗑️ BATCH 1 - DELETION CANDIDATES (3 Methods)

### 1. DeleteFilingComposerData
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/FilingComposerDataDelete.cs`
- **Line**: 29
- **Method Signature**:
```csharp
public static async Task DeleteFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `DeleteSourceData()` in `FilingComposerDataService.cs:235`
- **Action**: ✅ DELETE ENTIRE FILE or just the method

**Deletion Instructions**:
- Option A: Delete the entire file `FilingComposerDataDelete.cs` if this is its only method
- Option B: Delete lines 29-161 (the entire method including internal logic)
- Verify no other files import or reference this method

---

### 2. SaveFilingComposerData
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/FilingComposerDataSave.cs`
- **Line**: 31
- **Method Signature**:
```csharp
public static async Task SaveFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `SaveSourceData()` in `FilingComposerDataService.cs:79`
- **Action**: ✅ DELETE ENTIRE FILE or just the method

**Deletion Instructions**:
- Option A: Delete the entire file `FilingComposerDataSave.cs` if this is its only method
- Option B: Delete lines 31-412 (the entire method including internal logic)
- Verify no other files import or reference this method

---

### 3. CreateFilingComposerData
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/FilingComposerDataCreate.cs`
- **Line**: 29
- **Method Signature**:
```csharp
public static async Task CreateFilingComposerData(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `CreateSourceData()` in `FilingComposerDataService.cs:157`
- **Action**: ✅ DELETE ENTIRE FILE or just the method

**Deletion Instructions**:
- Option A: Delete the entire file `FilingComposerDataCreate.cs` if this is its only method
- Option B: Delete lines 29-326 (the entire method including internal logic)
- Verify no other files import or reference this method

---

### ⚠️ NOT ORPHANED - KEEP THIS ONE

#### RetrieveFilingData
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/FilingData.cs`
- **Line**: 149
- **Status**: ❌ **IN USE** - Called by `FilingDataController.cs:25`
- **Action**: **KEEP** - Do NOT delete
- **Note**: The REST controller still uses this method. Verify if REST endpoint is still active before deletion.

---

## 🗑️ BATCH 2 - DELETION CANDIDATES (1 Method)

### 1. RetrieveFilingDataOverview (HTTP Handler)
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/FilingComposerDataGet.cs`
- **Line**: 303
- **Method Signature**:
```csharp
public static async Task RetrieveFilingDataOverview(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `GetSourceDataOverview()` in `FilingComposerDataService.cs:112`
- **Action**: ✅ DELETE THIS METHOD ONLY

**Important**: There's a HELPER OVERLOAD at line 334 that MUST BE KEPT:
```csharp
public static TaxxorReturnMessage RetrieveFilingDataOverview(string dataFolderPathOs)
```
This helper is actively used by:
- `FilingComposerDataService.cs:535`
- `FilingComposerDataService.cs:684`
- `FilingComposerDataService.cs:740`
- `_Project.cs:708`

**Deletion Instructions**:
- Delete lines 303-332 (the HTTP handler overload only)
- Keep lines 334+ (the helper method overload)
- Be careful not to delete the helper method signature

---

### 2. CloneSectionContentLanguage
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/FilingComposerHierarchy.cs`
- **Line**: 659
- **Method Signature**:
```csharp
public static async Task CloneSectionContentLanguage(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ⚠️ ORPHANED - Never called
- **gRPC Replacement**: `CloneSectionLanguageData()` in `FilingComposerDataService.cs:314`
- **Action**: ✅ DELETE

**Deletion Instructions**:
- Delete lines 659-758 (the entire method)
- Verify the gRPC implementation is complete and working

---

## 🗑️ BATCH 3 - DELETION CANDIDATES (3 Methods)

All methods in `DocumentStore/DocumentStore/backend/controllers/cms/FilingComposerHierarchy.cs`

### 1. RetrieveFilingHierarchy
- **Line**: 25
- **Method Signature**:
```csharp
public static async Task RetrieveFilingHierarchy(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `LoadHierarchy()` in `FilingHierarchyService.cs:15`
- **Action**: ✅ DELETE

**Note**: This has been superseded by `RetrieveFilingHierarchyImproved`. Both should be deleted.

**Deletion Instructions**:
- Delete lines 25-83 (the entire method)

---

### 2. RetrieveFilingHierarchyImproved
- **Line**: 85
- **Method Signature**:
```csharp
public static async Task RetrieveFilingHierarchyImproved(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `LoadHierarchy()` in `FilingHierarchyService.cs:15`
- **Action**: ✅ DELETE

**Deletion Instructions**:
- Delete lines 85-352 (the entire method)

---

### 3. SaveFilingHierarchy
- **Line**: 354
- **Method Signature**:
```csharp
public static async Task SaveFilingHierarchy(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `SaveHierarchy()` in `FilingHierarchyService.cs:58`
- **Action**: ✅ DELETE

**Deletion Instructions**:
- Delete lines 354-657 (the entire method)

---

## 🗑️ BATCH 4 - DELETION CANDIDATES (2 Methods)

### 1. FindReplace
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/FindReplace.cs`
- **Line**: 25
- **Method Signature**:
```csharp
public async static Task FindReplace(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `FindReplace()` in `FilingDataUtilityService.cs:15`
- **Action**: ✅ DELETE ENTIRE FILE

**Deletion Instructions**:
- Delete the entire file `FindReplace.cs` if this is its only method
- Or delete lines 25-onwards (the entire method)

---

### 2. ClearCache
- **File**: `DocumentStore/DocumentStore/backend/controllers/cms/Utilities.cs`
- **Line**: 25
- **Method Signature**:
```csharp
public async static Task ClearCache(HttpRequest request, HttpResponse response, RouteData routeData)
```
- **Status**: ✅ ORPHANED - Never called
- **gRPC Replacement**: `ClearCache()` in `FilingDataUtilityService.cs:105`
- **Action**: ✅ DELETE THIS METHOD

**Important**: `Utilities.cs` may have other methods. Only delete the `ClearCache` method, not the entire file.

**Deletion Instructions**:
- Delete only the `ClearCache` method (lines 25-onwards)
- Check if there are other methods in `Utilities.cs` that should be kept

---

## 📋 Deletion Checklist

When deleting these methods, follow this process:

```
[ ] Batch 1:
  [ ] Delete DeleteFilingComposerData (FilingComposerDataDelete.cs:29-161)
  [ ] Delete SaveFilingComposerData (FilingComposerDataSave.cs:31-412)
  [ ] Delete CreateFilingComposerData (FilingComposerDataCreate.cs:29-326)
  [ ] SKIP RetrieveFilingData (FilingData.cs) - STILL IN USE

[ ] Batch 2:
  [ ] Delete RetrieveFilingDataOverview HTTP handler (FilingComposerDataGet.cs:303-332)
      - KEEP the helper method at line 334
  [ ] Delete CloneSectionContentLanguage (FilingComposerHierarchy.cs:659-758)

[ ] Batch 3:
  [ ] Delete RetrieveFilingHierarchy (FilingComposerHierarchy.cs:25-83)
  [ ] Delete RetrieveFilingHierarchyImproved (FilingComposerHierarchy.cs:85-352)
  [ ] Delete SaveFilingHierarchy (FilingComposerHierarchy.cs:354-657)

[ ] Batch 4:
  [ ] Delete FindReplace (FindReplace.cs - entire file or method)
  [ ] Delete ClearCache (Utilities.cs - method only)

[ ] Verification:
  [ ] Compile DocumentStore.sln - should succeed with no errors
  [ ] Compile Editor/TaxxorEditor.sln - should succeed with no errors
  [ ] Search codebase for any remaining references to deleted methods
  [ ] Test in Docker to verify gRPC implementations still work

[ ] After Verification:
  [ ] Commit with message: "Clean: Remove orphaned REST handler methods from Batches 1-4"
  [ ] Update MIGRATION_STATUS_TABLE.md to show "FULLY ORPHANED & DELETED"
```

---

## ⚠️ Important Notes

### Helper Method Preservation
The helper method overload at `FilingComposerDataGet.cs:334` MUST be preserved:
```csharp
public static TaxxorReturnMessage RetrieveFilingDataOverview(string dataFolderPathOs)
```

This is actively used by gRPC handlers. Only delete the HTTP handler overload at line 303, not this one.

### Method Dependencies
Some orphaned methods call other orphaned methods:
- `DeleteFilingComposerData` calls `RetrieveFilingDataOverview` (HTTP handler)
- `CreateFilingComposerData` calls `RetrieveFilingDataOverview` (HTTP handler)

When you delete the HTTP handler at line 303, these calls will break - but that's OK because the containing methods are also being deleted.

### RetrieveFilingData Exception
`RetrieveFilingData` in `FilingData.cs:149` is still actively used by `FilingDataController.cs:25`. Do NOT delete this method.

### CloneSectionContentLanguage Verification
This method appears to have been migrated to gRPC, but verify the gRPC implementation is complete before deletion:
- ✅ Check: `FilingComposerDataService.cs:314` has the gRPC handler
- ✅ Check: Editor client code calls the gRPC handler
- ✅ Check: REST endpoint definition is removed from ApiDispatcher.cs

---

## Summary Table

| Batch | Method | File | Lines | Status | Action |
|-------|--------|------|-------|--------|--------|
| 1 | DeleteFilingComposerData | FilingComposerDataDelete.cs | 29-161 | ORPHANED | DELETE |
| 1 | SaveFilingComposerData | FilingComposerDataSave.cs | 31-412 | ORPHANED | DELETE |
| 1 | CreateFilingComposerData | FilingComposerDataCreate.cs | 29-326 | ORPHANED | DELETE |
| 1 | RetrieveFilingData | FilingData.cs | 149 | IN USE | KEEP |
| 2 | RetrieveFilingDataOverview (HTTP) | FilingComposerDataGet.cs | 303-332 | ORPHANED | DELETE |
| 2 | RetrieveFilingDataOverview (Helper) | FilingComposerDataGet.cs | 334+ | IN USE | KEEP |
| 2 | CloneSectionContentLanguage | FilingComposerHierarchy.cs | 659-758 | ORPHANED | DELETE |
| 3 | RetrieveFilingHierarchy | FilingComposerHierarchy.cs | 25-83 | ORPHANED | DELETE |
| 3 | RetrieveFilingHierarchyImproved | FilingComposerHierarchy.cs | 85-352 | ORPHANED | DELETE |
| 3 | SaveFilingHierarchy | FilingComposerHierarchy.cs | 354-657 | ORPHANED | DELETE |
| 4 | FindReplace | FindReplace.cs | 25+ | ORPHANED | DELETE |
| 4 | ClearCache | Utilities.cs | 25+ | ORPHANED | DELETE |

---

## Next Steps

1. **Review this list** - Confirm all deletions are correct
2. **Make the deletions** - Remove all 11 orphaned methods
3. **Compile both solutions** - Ensure no build errors
4. **Search for references** - Use grep to confirm no other code references these methods
5. **Test in Docker** - Verify gRPC implementations still work
6. **Commit changes** - Clean up the orphaned code
7. **Update documentation** - Mark cleanup as complete

This will complete the cleanup effort and ensure no orphaned code remains from Batches 1-4.
