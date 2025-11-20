# TaxxorEditor

## File structure and GIT repositories

The TaxxorEditor application consists of a main repository with two git submodules and two nested git repositories. Here's the complete structure:

```
/Users/jthijs/Documents/my_projects/taxxor/tdm/services/Editor/
│
├── .git/                                    # Main repository: taxxoreditor
│   └── (origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxoreditor)
│
├── .gitmodules                              # Defines submodules configuration (2 submodules)
│
├── TaxxorEditor/                            # Main application directory
│   ├── backend/
│   │   ├── code/
│   │   │   ├── shared/                     # Git Submodule #1: taxxordotnetcoreshared
│   │   │   │   └── .git (file)             # (branch: master)
│   │   │   │       └── (origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordotnetcoreshared)
│   │   │   │
│   │   │   ├── custom/                     # Nested Git Repository #1: taxxordm-custom-backend
│   │   │   │   └── .git/ (directory)       # (separate repository, not a submodule)
│   │   │   │       └── (origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordm-custom-backend)
│   │   │   │
│   │   │   ├── extensions/                 # Extension code
│   │   │   ├── tasks/                      # Background tasks
│   │   │   └── [*.cs files]                # Main backend C# code files
│   │   │
│   │   ├── framework/                      # Git Submodule #2: dotnetcoreframework  
│   │   │   └── .git (file)                 # (no specific branch configured)
│   │   │       └── (origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/dotnetcoreframework)
│   │   │
│   │   ├── controllers/                    # MVC-style controllers
│   │   ├── middleware/                     # ASP.NET Core middleware
│   │   ├── routers/                        # Request routers
│   │   ├── grpc/                           # gRPC integration
│   │   └── views/                          # View templates
│   │
│   ├── frontend/
│   │   └── public/
│   │       └── custom/                     # Nested Git Repository #2: taxxordm-custom-frontend
│   │           └── .git/ (directory)       # (separate repository, not a submodule)
│   │               └── (origin: ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordm-custom-frontend)
│   │
│   ├── config/                              # XML configuration files
│   ├── data/                                # Application data
│   ├── hierarchies/                         # XML hierarchy definitions
│   ├── logs/                                # Application logs
│   ├── properties/                          # Project properties
│   └── typings/                             # TypeScript definitions
│
├── CustomerCodeInterface/                   # Customer code interface project (part of main repo)
├── GrpcServices/                            # gRPC service definitions (part of main repo)
│   └── Protos/                              # Protocol buffer definitions
│
├── Dockerfile                               # Container definition
├── TaxxorEditor.sln                         # Visual Studio solution file
├── TaxxorEditor.csproj                      # Main project file
├── package.json                             # Node.js dependencies
└── CLAUDE.md                                # Project documentation for Claude

```

## Repository Setup Instructions

To replicate this setup on another developer's machine:

1. **Clone the main repository:**
   ```bash
   git clone ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxoreditor
   cd taxxoreditor
   ```

2. **Initialize and update submodules:**
   ```bash
   git submodule init
   git submodule update
   ```

   Or in one command:
   ```bash
   git submodule update --init --recursive
   ```

3. **Clone the nested custom repositories separately:**
   ```bash
   # Backend custom repository
   cd TaxxorEditor/backend/code/
   rm -rf custom  # Remove any placeholder directory
   git clone ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordm-custom-backend custom
   
   # Frontend custom repository
   cd ../../frontend/public/
   rm -rf custom  # Remove any placeholder directory
   git clone ssh://git-codecommit.eu-west-1.amazonaws.com/v1/repos/taxxordm-custom-frontend custom
   
   # Return to root
   cd ../../../..
   ```

4. **Verify all repositories are properly cloned:**
   ```bash
   # Check submodules (should show .git files)
   ls -la TaxxorEditor/backend/code/shared/.git
   ls -la TaxxorEditor/backend/framework/.git
   
   # Check nested repositories (should show .git directories)
   ls -la TaxxorEditor/backend/code/custom/.git/
   ls -la TaxxorEditor/frontend/public/custom/.git/
   ```

## Repository Details

### Main Repository
- **Repository:** `taxxoreditor`
  - Contains the main TaxxorEditor application code
  - Includes Docker configuration, build scripts, and project files
  - Has `.gitmodules` file defining two submodules

### Git Submodules (managed by .gitmodules)
- **Submodule 1:** `taxxordotnetcoreshared` 
  - Location: `TaxxorEditor/backend/code/shared/`
  - Branch: master
  - Contains shared .NET Core code used across Taxxor applications
  - Identified by: `.git` is a file (not directory)

- **Submodule 2:** `dotnetcoreframework`
  - Location: `TaxxorEditor/backend/framework/`
  - Contains the core framework code for the application
  - Identified by: `.git` is a file (not directory)

### Nested Git Repositories (NOT submodules)
- **Nested Repository 1:** `taxxordm-custom-backend`
  - Location: `TaxxorEditor/backend/code/custom/`
  - Contains customer-specific backend code
  - Identified by: `.git` is a directory (not file)
  - Must be cloned separately

- **Nested Repository 2:** `taxxordm-custom-frontend`
  - Location: `TaxxorEditor/frontend/public/custom/`
  - Contains customer-specific frontend code
  - Identified by: `.git` is a directory (not file)
  - Must be cloned separately

## Important Notes

- All repositories are hosted on AWS CodeCommit in the eu-west-1 region
- SSH access is required for cloning (ensure your SSH keys are configured for AWS CodeCommit)
- The two submodules are managed by git and will be handled by `git submodule` commands
- The two nested repositories in `custom` directories are independent and must be cloned separately
- The nested repositories are not tracked by the main repository's `.gitmodules` file