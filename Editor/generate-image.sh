#!/bin/bash

application_version="0.0.0"
application_name="editor"
git_commit=""
repository_base_uri="518943790703.dkr.ecr.eu-west-1.amazonaws.com/taxxor"
timestamp=`date "+%Y%m%d%H%M%S"`


# Retrieve the latest GIT tag label and use that to create an explicit version tag for the image
echo ""
echo "- Retrieve version from GIT Tag."
application_version="$(git describe --tags $(git rev-list --tags --max-count=1) | sed 's#v##g')"
git_commit="$(git rev-parse --short HEAD)"
echo "* using version ${application_version} and commit ${git_commit} for tagging of Docker image"

# Retrieve the GIT branchname so that we can use it for tagging the image we are generating
echo ""
echo "- Retrieve Docker image tagname from from GIT branch."
branchname=`git branch | grep \* | cut -d ' ' -f2`
echo "* using branchname ${branchname} for tagging of Docker image"

# Copy certificates
echo ""
echo "- Copy certificate files."
cp ../../_utils/Certificates/Taxxor_root.crt ./TaxxorEditor/
cp ../../_utils/Certificates/editor.pfx ./TaxxorEditor/

# # Compile frontend assets for the editor
# echo ""
# echo "- Render Taxxor CMS frontend assets."
# gulp frontendfordocker

# Replace the application version with the in the XML file so that it's always in-line with the main repository
echo ""
echo "- Set version in configuration file."
sed -iE "s/\(<repository id=.application-root. version=.\).*\(. \/><repository id=.frame\)/\1$application_version\2/" "$( pwd )/TaxxorEditor/_repro-info.xml"
rm "$( pwd )/TaxxorEditor/_repro-info.xmlE"

# Copy the editor customizations to the source directory
rm -rf ./customercode
mkdir -p customercode
cp -R ../EditorCustomizations/compiled/customercode/* ./customercode/

#
# Build a multi-architecture image using buildx
#

# Determine the image tag that we need to use
case "$branchname" in
    master)
        manifesttag="production"
        ;;
    develop)
        manifesttag="staging"
        ;;
    *hotfix*)
        manifesttag="staging"
        ;;
    feature-*)
        manifesttag="feature"
        ;;
    *)
        manifesttag="$branchname"
        ;;
esac


    # --cache-from=type=local,src=$HOME/.docker/buildx-cache \
    # --cache-to=type=local,dest=$HOME/.docker/buildx-cache \

# Build the docker file
echo ""
echo "- Build the Docker image and tag it."
docker buildx use amd-builder
docker buildx build \
--cache-from=type=registry,ref=${repository_base_uri}/${application_name}:buildcache \
--cache-to=type=registry,ref=${repository_base_uri}/${application_name}:buildcache,mode=max \
--platform=linux/amd64,linux/arm64 \
--push \
-f Dockerfile \
--label com.taxxor.version=${application_version} \
--label com.taxxor.commit=${git_commit} \
-t ${repository_base_uri}/${application_name}:${manifesttag} \
.

# Pull the generated image so that we can use docker scout to inspect it
# if [[ "$manifesttag" == "staging" ]]; then
#     docker pull ${repository_base_uri}/${application_name}:${manifesttag}
# fi

# Remove the editor customizations
rm -rf ./customercode


# exit 0