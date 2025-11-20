
# Base variables
application_root_path_os="$( pwd )"
parent_path_os="$( dirname $( pwd ) )"
submodules=( "/TaxxorEditor/backend/framework" "/TaxxorEditor/backend/code/shared" )
nestedrepros=( "/TaxxorEditor/backend/code/custom" )

# Retrieve the GIT branchname so that we can use it for tagging the image we are generating
echo ""
echo "- Retrieve current branchname."
branchname=`git branch | grep \* | cut -d ' ' -f2`
echo "* using branchname ${branchname} for tagging of Docker image"

# Only contnue if we are not on master
if [[ "$branchname" == "master" ]]; then
    echo ""
    echo "ERROR: Cannot execute this script because we are on the master branch!!"
else
    echo ""
    echo "- Switch to master branch."

    # Switch to master branch
    git --no-optional-locks checkout master 

    # Pull to make sure that we receive the latest content before we start the merge process
    # git --no-optional-locks pull

    # Update the submodules
    git --no-optional-locks submodule update --init 

    # Merge with master the develop branch
    git --no-optional-locks merge develop 

    #
    # Update the status of the submodules
    #
    echo ""
    echo "- Update the submodules."
    for i in "${submodules[@]}"
    do
        submodulepathos="${application_root_path_os}${i}"
        echo ""
        echo "* Preparing submodule ${i}"
        echo ""

        # Checkout master branch
        git -C $submodulepathos --no-optional-locks checkout master 

        # Update and init
        git -C $submodulepathos --no-optional-locks submodule update --init

        # Pull the latest status for this submodule
        git -C $submodulepathos --no-optional-locks fetch origin 
        git -C $submodulepathos --no-optional-locks pull origin master 
    done

    #
    # Push the new status of the master branch to the GIT repository
    #
    remotename=`git remote show`
    echo ""
    echo "- Pushing the updated master branch to remote ${remotename}."
    git --no-optional-locks push -v --tags $remotename refs/heads/master:refs/heads/master 

    #
    # Update the nested GIT repositories
    #
    echo ""
    echo "- Update the nested GIT repositories."
    for i in "${nestedrepros[@]}"
    do
        submodulepathos="${application_root_path_os}${i}"
        echo ""
        echo "* Pulling nested repository ${i}"
        echo ""

        branchname=`git -C ${submodulepathos} branch | grep \* | cut -d ' ' -f2`
        remotename=`git -C ${submodulepathos} remote show`
        echo "* using branchname ${branchname} for and remote ${remotename} pulling nested GIT repository"

        git -C $submodulepathos --no-optional-locks fetch $remotename 
        git -C $submodulepathos --no-optional-locks pull $remotename $branchname
    done  

    #
    # Generate the image
    #
    echo ""
    echo "- Generate the image."
    . generate-image.sh


    #
    # Switch back to develop
    #
    echo ""
    echo "- Switch back to the develop branch."
    git --no-optional-locks checkout develop 
fi
