# DocumentStore Service

## File Structure and GIT Repositories

### Git Repository Structure Analysis

This repository contains **1 main repository** and **3 git submodules**. All submodules are properly configured in `.gitmodules` and managed through git submodule commands.


### Directory Structure with Git Repositories

```
DocumentStore/                                          [MAIN REPOSITORY - .git directory]
│                                                       Type: Separate git repository
│                                                       Origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordocumentstore
│                                                       Current Branch: develop
│
├── .git/                                               [Git Directory - Main Repository Database]
├── .gitmodules                                         [Submodule Configuration File]
│
├── _config/                                            # Configuration files
├── DocumentStore/                                      # Main application directory
│   ├── backend/                                        # Backend code and framework
│   │   ├── code/                                       # Application code
│   │   │   ├── shared/                                [SUBMODULE - .git file]
│   │   │   │   ├── .git                              Type: Git submodule (file)
│   │   │   │                                          gitdir: ../../../../.git/modules/DocumentStore/backend/code/shared
│   │   │   │                                          Origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordotnetcoreshared
│   │   │   │                                          Branch: master
│   │   │   │                                          Commit: bf1f3a2 (version 1.186.1-5-gbf1f3a2)
│   │   │   └── ...                                    # Shared C# libraries
│   │   │
│   │   ├── framework/                                 [SUBMODULE - .git file]
│   │   │   ├── .git                                  Type: Git submodule (file)
│   │   │                                              gitdir: ../../../.git/modules/DocumentStore/backend/framework
│   │   │                                              Origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/dotnetcoreframework
│   │   │                                              Branch: master
│   │   │                                              Commit: cabc60c (version 4.3.2-1-gcabc60c)
│   │   │                                              # Core framework components
│   │   │
│   │   ├── controllers/                               # MVC controllers
│   │   ├── middleware/                                # Middleware components
│   │   ├── routers/                                   # Routing configuration
│   │   └── views/                                     # View templates
│   │
│   ├── config/                                         # XML configuration files
│   │   └── base_configuration.xml                     # Core application config
│   │
│   ├── Controllers/                                    # C# MVC Controllers
│   ├── data/                                           # Application data storage
│   ├── frontend/                                       # Frontend assets (not actively used)
│   ├── hierarchies/                                    # XML hierarchy definitions
│   ├── logs/                                           # Application logs
│   ├── Models/                                         # C# MVC Models
│   ├── Services/                                       # C# MVC Services
│   │
│   ├── templates/                                     [SUBMODULE - .git file]
│   │   ├── .git                                      Type: Git submodule (file)
│   │                                                  gitdir: ../../.git/modules/DocumentStore/templates
│   │                                                  Origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxorprojectdatastoretemplates
│   │                                                  Branch: Not specified in .gitmodules (defaults to HEAD)
│   │                                                  Commit: 92a5159 (version 1.38.0)
│   │                                                  # Project and configuration templates
│   │
│   ├── temp/                                           # Temporary files
│   ├── properties/                                     # Project properties
│   ├── Program.cs                                      # Application entry point
│   ├── Startup.cs                                      # Startup configuration
│   └── DocumentStore.csproj                            # Project file
│
├── GrpcServices/                                        # gRPC service definitions
├── Dockerfile                                           # Docker container definition
├── gulpfile.js                                          # Build tasks
├── package.json                                         # Node.js dependencies
└── DocumentStore.sln                                    # Solution file
```

## Repository Setup Instructions

To replicate this setup on another developer's machine:

### 1. Clone the main repository:

```bash
git clone ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordocumentstore
cd taxxordocumentstore
```

### 2. Initialize and update submodules:

```bash
git submodule init
git submodule update
```

Or in one command:

```bash
git submodule update --init --recursive
```

### 3. Verify all repositories are properly cloned:

```bash
# Check submodules (should show .git files)
ls -la DocumentStore/backend/code/shared/.git
ls -la DocumentStore/backend/framework/.git
ls -la DocumentStore/templates/.git

# All above should show .git as files (not directories)
```

**Note:** This repository only uses git submodules. There are no nested custom repositories that require separate cloning.
