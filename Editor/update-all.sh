
# Base variables
application_root_path_os="$( pwd )"
parent_path_os="$( dirname $( pwd ) )"
submodules=( "/backend/framework" "/backend/code/shared" )
nestedrepros=( "/backend/code/custom" "/frontend/public/custom" )

# Retrieve the GIT branchname so that we can use it for tagging the image we are generating
echo ""
echo "- Retrieve current branchname."
branchname=`git branch | grep \* | cut -d ' ' -f2`
echo "* using branchname ${branchname}"

#
# Pull the main repository
#
echo ""
echo "- Update main repository."
git --no-optional-locks fetch origin 
git --no-optional-locks pull origin $branchname


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