# gRPC Migration Plan

## Current Status

**Last Updated**: [Update this with each commit]  
**Current Batch**: Batch 1  
**Completed Batches**: 0/6  
**Total Methods Migrated**: 0/16

---

## Batch Overview

| Batch | Methods | Status | Commit Hash |
|-------|---------|--------|-------------|
| Batch 1 | SaveSourceData, DeleteSourceData, CreateSourceData | ⬜ Not Started | - |
| Batch 2 | SourceDataOverview, CloneSectionLanguageData | ⬜ Not Started | - |
| Batch 3 | LoadHierarchy, SaveHierarchy | ⬜ Not Started | - |
| Batch 4 | FindReplace, ClearCache | ⬜ Not Started | - |
| Batch 5 | GeneratedReportsRepository methods | ⬜ Not Started | - |
| Batch 6 | VersionControl methods | ⬜ Not Started | - |

**Status Legend**: ⬜ Not Started | 🟡 In Progress | ✅ Complete

---

## Batch Details

### Batch 1: Core CRUD Operations
**Priority**: HIGH (foundational methods)  
**Status**: ⬜ Not Started

#### Methods to Migrate

- [ ] **SaveSourceData** - Save filing editor source data to DocumentStore
  - REST endpoint: `taxxoreditorcomposerdata` (POST)
  - Parameters: xmlDoc, id, contentLanguage
  - Returns: XmlDocument

- [ ] **DeleteSourceData** - Delete a filing section source data fragment
  - REST endpoint: `taxxoreditorcomposerdataextended` (POST)
  - Parameters: xmlDeleteActions, projectId, versionId, dataType, contentLanguage
  - Returns: XmlDocument

- [ ] **CreateSourceData** - Create a new source data section
  - REST endpoint: `taxxoreditorcomposerdata` (PUT)
  - Parameters: sectionId, xmlDoc, projectId, versionId, dataType, contentLanguage
  - Returns: XmlDocument with section overview

#### Proto Service Name
`FilingDataService` (add to existing or create new)

#### REST Cleanup
- Remove `taxxoreditorcomposerdata` POST/PUT routes from ApiDispatcher.cs
- Remove `taxxoreditorcomposerdataextended` POST route from ApiDispatcher.cs
- Remove corresponding XML definitions from base_structure.xml

#### Completion Checklist
- [ ] Proto definitions added
- [ ] Server handlers implemented
- [ ] Editor clients updated
- [ ] Both solutions compile
- [ ] REST code removed
- [ ] Committed and pushed

---

### Batch 2: Data Operations
**Priority**: MEDIUM  
**Status**: ⬜ Not Started

#### Methods to Migrate

- [ ] **SourceDataOverview** - Get overview of all section XHTML files in project
  - REST endpoint: `taxxoreditorcomposerdataoverview` (GET)
  - Parameters: projectId, versionId
  - Returns: XmlDocument with section overview

- [ ] **CloneSectionLanguageData** - Clone content from one language to another
  - REST endpoint: `taxxoreditorcontentlanguageclone` (GET)
  - Parameters: projectId, did, sourceLang, targetLang, includeChildren
  - Returns: XmlDocument

#### Proto Service Name
`FilingDataService`

#### REST Cleanup
- Remove `taxxoreditorcomposerdataoverview` GET route from ApiDispatcher.cs
- Remove `taxxoreditorcontentlanguageclone` GET route from ApiDispatcher.cs
- Remove corresponding XML definitions from base_structure.xml

#### Completion Checklist
- [ ] Proto definitions added
- [ ] Server handlers implemented
- [ ] Editor clients updated
- [ ] Both solutions compile
- [ ] REST code removed
- [ ] Committed and pushed

---

### Batch 3: Hierarchy Management
**Priority**: HIGH (core feature)  
**Status**: ⬜ Not Started

#### Methods to Migrate

- [ ] **LoadHierarchy** - Load filing hierarchy from DocumentStore
  - REST endpoint: `taxxoreditorcomposerhierarchy` (GET)
  - Parameters: projectId, versionId, editorId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage
  - Returns: XmlDocument with hierarchy

- [ ] **SaveHierarchy** - Store filing hierarchy in DocumentStore
  - REST endpoint: `taxxoreditorcomposerhierarchy` (POST)
  - Parameters: hierarchy, projectId, versionId, editorId, outputChannelType, outputChannelVariantId, outputChannelVariantLanguage, commitChanges
  - Returns: XmlDocument

#### Proto Service Name
`FilingHierarchyService` (new service)

#### REST Cleanup
- Remove `taxxoreditorcomposerhierarchy` GET/POST routes from ApiDispatcher.cs
- Remove corresponding XML definitions from base_structure.xml

#### Completion Checklist
- [ ] Proto definitions added
- [ ] Server handlers implemented
- [ ] Editor clients updated
- [ ] Both solutions compile
- [ ] REST code removed
- [ ] Committed and pushed

---

### Batch 4: Utility Operations
**Priority**: LOW  
**Status**: ⬜ Not Started

#### Methods to Migrate

- [ ] **FindReplace** - Find and replace text in all project data files
  - REST endpoint: `findreplace` (POST)
  - Parameters: searchFragment, replaceFragment, onlyInUse, includeFootnotes, dryRun
  - Returns: XmlDocument

- [ ] **ClearCache** - Clear the memory cache on DocumentStore service
  - REST endpoint: `clearcache` (DELETE)
  - Parameters: projectVars
  - Returns: TaxxorReturnMessage

#### Proto Service Name
`FilingDataUtilityService` (new service)

#### REST Cleanup
- Remove `findreplace` POST route from ApiDispatcher.cs
- Remove `clearcache` DELETE route from ApiDispatcher.cs
- Remove corresponding XML definitions from base_structure.xml

#### Completion Checklist
- [ ] Proto definitions added
- [ ] Server handlers implemented
- [ ] Editor clients updated
- [ ] Both solutions compile
- [ ] REST code removed
- [ ] Committed and pushed

---

### Batch 5: Generated Reports Repository
**Priority**: MEDIUM  
**Status**: ⬜ Not Started

#### Methods to Migrate

- [ ] **GeneratedReportsRepository.Add** - Add generated report to repository
  - REST endpoint: `generatedreportsrepository` (PUT)
  - Parameters: path, reportRequirementScheme, xbrlValidationInformation
  - Returns: TaxxorReturnMessage

- [ ] **GeneratedReportsRepository.RetrieveContent** - Get repository content
  - REST endpoint: `generatedreportsrepository` (GET)
  - Parameters: filterScheme, filterUser, filterGuid
  - Returns: TaxxorReturnMessage

#### Proto Service Name
`GeneratedReportsRepositoryService` (new service)

#### REST Cleanup
- Remove `generatedreportsrepository` PUT/GET routes from ApiDispatcher.cs
- Remove corresponding XML definitions from base_structure.xml

#### Completion Checklist
- [ ] Proto definitions added
- [ ] Server handlers implemented
- [ ] Editor clients updated
- [ ] Both solutions compile
- [ ] REST code removed
- [ ] Committed and pushed

---

### Batch 6: Version Control Operations
**Priority**: LOW (used less frequently)  
**Status**: ⬜ Not Started

#### Methods to Migrate

- [ ] **VersionControl.GitDiffBetweenCommits** - Get diff between two commits
  - REST endpoint: `gitdiff` (POST)
  - Parameters: projectId, locationId, baseCommitHash, latestCommitHash, gitFilePath
  - Returns: TaxxorReturnMessage

- [ ] **VersionControl.GitExtractSingleFile** - Extract single file from commit
  - REST endpoint: `gitextractsingle` (POST)
  - Parameters: projectId, locationId, commitHash, sourceFilePath, extractLocationFolderPath
  - Returns: TaxxorReturnMessage

- [ ] **VersionControl.GitExtractAll** - Extract all files from commit
  - REST endpoint: `gitextractall` (POST)
  - Parameters: projectId, locationId, commitHash, extractLocationFolderPathOs
  - Returns: TaxxorReturnMessage

- [ ] **VersionControl.GitCommit** - Create a commit in version control
  - REST endpoint: `gitcommit` (POST)
  - Parameters: projectId, locationId, message
  - Returns: TaxxorReturnMessage

#### Proto Service Name
`VersionControlService` (new service)

#### REST Cleanup
- Remove `gitdiff`, `gitextractsingle`, `gitextractall`, `gitcommit` POST routes from ApiDispatcher.cs
- Remove corresponding XML definitions from base_structure.xml

#### Completion Checklist
- [ ] Proto definitions added
- [ ] Server handlers implemented
- [ ] Editor clients updated
- [ ] Both solutions compile
- [ ] REST code removed
- [ ] Committed and pushed

---

## Notes

### Additional Context Not Migrated Yet
**LoadCompleteSourceData** - Currently only reads from XML configuration, doesn't call REST API. Can be migrated later if needed.

### Already Migrated
✅ **LoadSourceData** - Already uses gRPC via FilingComposerDataService

### Testing Strategy
After each batch is completed and committed:
1. Pull changes to local machine
2. Run split script
3. Test both services in local Docker environment
4. Verify functionality with actual project data
5. If successful, commit to original repos and proceed to next batch

### Rollback Plan
If a batch causes issues:
1. Revert the commit in the monorepo
2. Push the revert to GitHub
3. Pull and split again
4. Fix issues and retry
