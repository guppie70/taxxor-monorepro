#!/bin/bash

echo "*****************************************"
echo "Starting DocumentStore app in docker container"
echo "- entrypoint: /app/entrypoint.sh"
echo "*****************************************"

#
# -> Optionally create symlinks
#
if [ -f "/app/entrypoint.symlinks.sh" ]; then
    echo ""
    echo "=> Sourcing symlink creation script: /app/entrypoint.symlinks.sh"
    source "/app/entrypoint.symlinks.sh"
else
    echo "=> Skipping symlink creation script: /app/entrypoint.symlinks.sh does not exist"
fi

#
# -> Configure GIT
#
echo ""
echo "=> configuring GIT"
git config --global safe.directory "*"
git config --global pull.rebase false


#
# -> Start the application
#
echo ""
echo "=> Starting application"
dotnet DocumentStore.dll