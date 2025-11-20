#
# Calls the deploy routine to update Taxxor docker stacks on target servers
#
current_dir="$(pwd)"
deploy_utility_dir="$( dirname $( dirname $( pwd ) ) )/_utils/update-docker"

# echo "DEBUG"
# echo "current_dir: ${current_dir}"
# echo "deploy_utility_dir: ${deploy_utility_dir}"

cd $deploy_utility_dir
bash _production-deploy.sh
cd $current_dir