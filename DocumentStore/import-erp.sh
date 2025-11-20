#
# Imports Taxxor DM data from a remote server using SSH and rsync
# Script dynamically reads which environment is running on this machine by inspecting the Docker Compose environment file
# Using this information, the scripts connects to the correct remote server to start the rsync import
#
basedir=""
localdir="$( dirname $( dirname $( pwd ) ) )"
taxxorserverid=""


#
# Load the common functions
#
. "$( pwd )/_import-data-functions.sh"

# 
# Retrieve the client ID from the Docker Compose environment file
# 
retrieve_serverid "$localdir"
taxxorserverid="$REPLY"

# 
# Retrieve the remote server address
# 
retrieve_remote_server_address "$taxxorserverid" "production"
basedir="$REPLY"

# 
# Start the importing process
#
echo "" 
echo "-----------------------------------------------------------------"
echo ""
echo "Start importing ERP data from ${taxxorserverid} server"
echo ""
echo "HOSTSERVER_ID (from Docker Compose .env file): ${taxxorserverid}"
echo "Remote location: ${basedir}"
echo "Local directory used: ${localdir}"
echo ""

rsync -avh --exclude 'PhilipsEfrDataService/*' --exclude 'zookeeper/version-2/log.*' --progress --delete -e ssh ${basedir}/data/PhilipsEfrDataService/ ${localdir}/data/${taxxorserverid}/PhilipsEfrDataService


echo ""
echo ""
echo "Done"
echo ""
echo "-----------------------------------------------------------------"
echo ""