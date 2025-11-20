#
# Pushes Taxxor DM data from a local development machine to a remote server using SSH and rsync
# The script pushes the Project Data Store contents only
# !! NOTE: this script will overwrite the content of the Taxxor Project Data Store on the remote server and can potentially be very destructive !!
# Script dynamically reads which environment is running on this machine by inspecting the Docker Compose environment file
# Using this information, the scripts connects to the correct remote server to start the rsync import
#
basedir=""
localdir="$( dirname $( dirname $( pwd ) ) )"
taxxorclientid=""


#
# Load the common functions
#
. "$( pwd )/_import-data-functions.sh"

# 
# Retrieve the client ID from the Docker Compose environment file
# 
retrieve_serverid "$localdir"
taxxorclientid="$REPLY"

# 
# Retrieve the remote server address
# 
retrieve_remote_server_address "$taxxorclientid" "production"
basedir="$REPLY"

#
# Calculate the locations we need to use for the push command
#
pushbasedir="${basedir}/data/DocumentStore"
pushlocaldir="${localdir}/data/${taxxorclientid}/DocumentStore/"


# 
# Start the importing process
#
echo "" 
echo "-----------------------------------------------------------------"
echo ""
echo "Start pushing Project Datastore content from the local machine to the ${taxxorclientid^} server"
echo ""
echo "CLIENT ID (from Docker Compose .env file): ${taxxorclientid}"
echo "Remote location: ${pushbasedir}"
echo "Local directory used: ${pushlocaldir}"
echo ""

echo "This will overwrite all the contents of the Project Data Store on the ${taxxorclientid^} server. Are you sure you want to proceed [Yes | No]?"

if [ -n "$ZSH_VERSION" ]; then
  read -r "answer?"
else
  read -r answer
fi

if [[ "$answer" != "Yes" ]]; then
    echo "Aborting push data routine (received ${REPLY} from user prompt)"
    exit 1
fi

rsync -avh --exclude '.DS_Store' --progress --delete -e ssh ${pushlocaldir} ${pushbasedir}

# Also sync the AccessControlService data
pushbasedir="${basedir}/data/AccessControlService"
pushlocaldir="${localdir}/data/${taxxorclientid}/AccessControlService/"

rsync -avh --exclude '.DS_Store' --progress --delete -e ssh ${pushlocaldir} ${pushbasedir}

# Also sync the configuration directory
pushbasedir="${basedir}/config"
pushlocaldir="${localdir}/config/${taxxorclientid}/"

rsync -avh --exclude '.DS_Store' --progress --delete -e ssh ${pushlocaldir} ${pushbasedir}

echo ""
echo ""
echo "Done"
echo ""
echo "-----------------------------------------------------------------"
echo ""