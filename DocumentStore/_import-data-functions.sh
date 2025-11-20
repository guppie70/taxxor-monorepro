#
# Functions used for the data import scripts
# Use reserved REPLY variable to return values from functions (https://stackoverflow.com/a/34616619/3819725)
#

#
# Retrieves the server ID from the Docker Compose environment file
#
retrieve_serverid() {
    taxxorserverid=""
    taxxorrootdirectory="$1"
    dockercomposeenvfilepathos="${taxxorrootdirectory}/.env"

    # echo "Read ENV data file from Docker Compose (path: ${dockercomposeenvfilepathos})"
    # echo ""

    #
    # Test if we can find the Docker Compose environment file
    #
    if [ -f $dockercomposeenvfilepathos ]; then

        #
        # Read the Docker Compose file line by line
        #
        while IFS= read -r line; do
            # echo "-${line}-"

            #
            # Grab the environment variable name and if it matches "HOSTSERVER_ID", then fill the taxxorserverid variable with it
            #
            variablename=$(echo "$line" | grep -v '^#|=' | sed -E 's/(.*)=.*/\1/')

            if [[ "$variablename" == "HOSTSERVER_ID" ]]; then
                taxxorserverid=$(echo "$line" | grep -v '^#|=' | sed -E 's/.*=(.*)/\1/')
            fi

            # echo "variablename=${variablename}"

        done <"${dockercomposeenvfilepathos}"
    else
        echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        echo "ERROR: unable to find ${dockercomposeenvfilepathos}"
        echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        exit 1
    fi

    #
    # Exit the routine if the client id is empty
    #
    if [[ "$taxxorserverid" == "" ]]; then
        echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        echo "ERROR: unable to parse a HOSTSERVER_ID value from the Docker Compose environment file (${dockercomposeenvfilepathos})"
        echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        exit 1
    fi

    REPLY="${taxxorserverid}"
}

#
# Based on the client id, define the remote SSH path to the Taxxor DM server
#
retrieve_remote_server_address() {
    taxxorserverid="$1"
    environment="$2"
    basedir=""
    privatekey="~/.ssh/tx-utils-keypair"

    # Check if environment is staging and change port number if needed
    portnumber="4222"
    if [[ "$environment" == "staging" ]]; then
        portnumber="5222"
    fi

    # Dynamically select the SSH host that we use to jump to the TA Utils container
    jumphost=""
    case $taxxorserverid in
        "taxxor") jumphost="tdm-taxxor" ;;
        "philips") jumphost="tdm-philips" ;;
        "mazars") jumphost="tdm-mazars" ;;
        "domesticappliances") jumphost="tdm-taxxor,tdm-versuni" ;;
        "tiekinetix") jumphost="tdm-tie" ;;
        "azerion") jumphost="tdm-taxxor,tdm-azerion" ;;
        "crelan") jumphost="tdm-taxxor" ;;
        "vastned") jumphost="tdm-vastned" ;;
        "optiver") jumphost="tdm-optiver" ;;
        "cvo") jumphost="tdm-cvo" ;;
        "havensteder") jumphost="tdm-havensteder" ;;
        *) jumphost="tdm-taxxor" ;;
    esac

    #
    # Exit the routine if we could not find a remote address
    #
    if [[ "$taxxorserverid" == "" ]]; then
        echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        echo "ERROR: unable to find a jump host for the Taxxor DM server (taxxorserverid=${taxxorserverid})"
        echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
        exit 1
    fi


    cat >./rsync-ssh-config <<EOF
Include ~/.ssh/config

Host temp-target
    HostName localhost
    User root
    Port ${portnumber}
    IdentityFile ${privatekey}
    StrictHostKeyChecking no
    UserKnownHostsFile=/dev/null
    ProxyJump ${jumphost}
EOF

}
