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

# 
# Start the importing process
#
echo "" 
echo "-----------------------------------------------------------------"
echo ""
echo "Start importing data from ${taxxorserverid} server"
echo ""
echo "HOSTSERVER_ID (from Docker Compose .env file): ${taxxorserverid}"
echo "Local directory used: ${localdir}"
echo ""

# Echo the command that was just executed
echo "rsync -avhz --batch-size=1000 --progress --delete -e \"ssh -F ./rsync-ssh-config\" temp-target:/workspace/data/ ${localdir}/data/${taxxorserverid}"

rsync -avhz \
    --max-alloc=500M --block-size=4096 \
    --exclude 'PhilipsEfrDataService/*' \
    --exclude 'PdfService/_debug/*' \
    --exclude 'MappingService/Mapping-*.db' \
    --exclude 'MappingService/Mapping_*.db' \
    --exclude 'Xslt2Service/jobs/*' \
    --exclude 'reports/*.zip' \
    --exclude 'db.lck' \
    --exclude 'zookeeper/version-2/log.*' \
    --exclude '_shared' \
    --exclude 'devpackages' \
    --exclude 'repositories' \
    --exclude 'StructuredDataStore/data_*.db' \
    --exclude 'StructuredDataStore/data-*.db' \
    --exclude 'DocumentStore/projects/*/*/content/system/cache/*' \
	--exclude 'DocumentStore/projects/*/*/content/reports/*.zip' \
    --exclude 'StaticAssets/repository' \
    --progress \
    --delete \
    -e "ssh -F ./rsync-ssh-config" temp-target:/workspace/data/ ${localdir}/data/${taxxorserverid}

rsync -avhz \
    --max-alloc=500M --block-size=4096 \
    --exclude 'CodeServer/*' \
    --progress \
    --delete \
    -e "ssh -F ./rsync-ssh-config" temp-target:/workspace/config/ ${localdir}/config/${taxxorserverid}

echo ""
echo ""
echo "Done"
echo ""
echo "-----------------------------------------------------------------"
echo ""