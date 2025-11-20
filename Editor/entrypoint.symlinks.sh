#!/bin/bash

#
# Creates symlinks inside a docker container
# Part of the application startup routine
#

if [ -d "/mnt/devpackages" ]; then
    echo ""
    echo "=> Create Report Design packages symlinks"

    echo "   - Cleanup public outputchannels folder"
    rm -rf "/app/TaxxorEditor/frontend/public/outputchannels"
    mkdir -p "/app/TaxxorEditor/frontend/public/outputchannels/${HOSTSERVER_ID}"

    echo "   (1) Report Design Package root"
    rm -rf /app/TaxxorEditor/frontend/devpackages
    ln -s /mnt/devpackages /app/TaxxorEditor/frontend/devpackages

    # Array of output channel types
    output_channels=("pdf" "htmlsite" "website")
    
    # Loop through the output channel types
    i=1
    for channel in "${output_channels[@]}"; do
        
    
        if [ -d "/mnt/devpackages/${HOSTSERVER_ID}/${channel}/_compiled" ]; then
            echo "   ($((i+1))) Symlink ${channel} assets from /mnt/devpackages/${HOSTSERVER_ID}/${channel}/_compiled/ to /app/TaxxorEditor/frontend/public/outputchannels/${HOSTSERVER_ID}/${channel}"
            ln -s "/mnt/devpackages/${HOSTSERVER_ID}/${channel}/_compiled/" "/app/TaxxorEditor/frontend/public/outputchannels/${HOSTSERVER_ID}/${channel}"
        else
            echo "   Skipping symlink creation: /mnt/devpackages/${HOSTSERVER_ID}/${channel}/_compiled does not exist"
        fi

        # Increment counter variable
        ((i++))
    done

    # Static assets directories to create and symlink
    echo ""
    echo "=> Create static assets directories and symlinks"
    
    # Array of static asset types
    static_assets=("stylesheets" "javascript")
    
    # Loop through static asset types
    for asset_type in "${static_assets[@]}"; do
        asset_dir="/mnt/devpackages/staticassets/${asset_type}"
        symlink_target="/app/TaxxorEditor/frontend/public/outputchannels/${asset_type}"
        
        # Create directory if it doesn't exist
        if [ ! -d "${asset_dir}" ]; then
            echo "   Creating ${asset_dir} directory"
            mkdir -p "${asset_dir}"
        else
            echo "   Directory ${asset_dir} already exists"
        fi
        
        # Create symlink to outputchannels
        echo "   Creating symlink from ${asset_dir} to ${symlink_target}"
        rm -rf "${symlink_target}"
        ln -s "${asset_dir}" "${symlink_target}"
    done

    # Set AWS_PROFILE based on HOSTSERVER_ID
    if [ -z "$HOSTSERVER_ID" ]; then
        echo "HOSTSERVER_ID environment variable is not set, setting AWS_PROFILE to 'taxxor'"
        export AWS_PROFILE="taxxor"
    elif [ "$HOSTSERVER_ID" = "philips" ]; then
        echo "HOSTSERVER_ID is 'philips', setting AWS_PROFILE to 'default'"
        export AWS_PROFILE="default"
    else
        echo "HOSTSERVER_ID is '$HOSTSERVER_ID', setting AWS_PROFILE to 'taxxor'"
        export AWS_PROFILE="taxxor"
    fi

else
    echo "Skip creation of Report Design Package symlinks (folder /mnt/devpackages does not exist)"
fi 

#
# Create a symlink of the complete "keys" folder in /app/TaxxorEditor/secrets
#
if [ -d "/mnt/keys" ]; then
    echo "Creating secrets symlink"
    rm -rf "/app/TaxxorEditor/secrets"
    mkdir -p "/app/TaxxorEditor/secrets"
    ln -s "/mnt/keys" "/app/TaxxorEditor/secrets"
else
    echo "Skip of keys symlink (folder /mnt/keys does not exist)"
fi

#
# Public downloads (Taxxor instance only)
#
if [ -d "/mnt/downloads" ]; then
    echo "Creating downloads symlink"
    rm -rf "/app/TaxxorEditor/frontend/public/files/downloads"
    mkdir -p "/app/TaxxorEditor/frontend/public/files"
    ln -s "/mnt/downloads" "/app/TaxxorEditor/frontend/public/files/downloads"
else
    echo "Skip of downloads symlink (folder /mnt/downloads does not exist)"
fi

#
# Copy the contents of .ssh 
#
if [ -d "/mnt/keys/.ssh" ]; then
    # Copy all the files in /mnt/keys/.ssh to /home/aspnet/.ssh 
    echo "Copying SSH keys to /home/aspnet/.ssh"
    mkdir -p /home/aspnet/.ssh
    cp -r /mnt/keys/.ssh/* /home/aspnet/.ssh/
    chown -R aspnet:aspnet /home/aspnet/.ssh
    chmod 700 /home/aspnet/.ssh
    chmod 600 /home/aspnet/.ssh/*
else
    echo "Skip copying SSH keys (folder /mnt/keys/.ssh does not exist)"
fi

#
# Copy the contents of .aws
#
if [ -d "/mnt/keys/.aws" ]; then
    # Copy all the files in /mnt/keys/.aws to /home/aspnet/.aws
    echo "Copying AWS credentials to /home/aspnet/.aws"
    mkdir -p /home/aspnet/.aws
    cp -r /mnt/keys/.aws/* /home/aspnet/.aws/
    chown -R aspnet:aspnet /home/aspnet/.aws
    chmod 700 /home/aspnet/.aws
    chmod 600 /home/aspnet/.aws/*
else
    echo "Skip copying AWS credentials (folder /mnt/keys/.aws does not exist)"
fi