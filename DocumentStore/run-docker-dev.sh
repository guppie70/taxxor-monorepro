#
# This needs to be worked out - the concept here is to map the working directory of node in an "empty" container so that you develop the application on your local machine but let Docker serve the application
#
exposed_port="4813"
host_ip_address=`ifconfig $(netstat -rn | grep -E "^default|^0.0.0.0" | head -1 | awk '{print $NF}') | grep 'inet ' | awk '{print $2}' | grep -Eo '([0-9]*\.){3}[0-9]*'`

docker run \
    -it \
    --rm \
    --entrypoint /bin/sh \
	-e HOST_IP="${host_ip_address}" \
    -e ASPNETCORE_ENVIRONMENT="Production" \
	-p "${exposed_port}":4813 \
    -p 5854:5854 \
	--expose 4813 \
    -v ~/.ssh:/root/.ssh \
    -v ~/.aws:/root/.aws \
    --name taxxor${application_name}dev \
    editor:latest

#
# Notes:
# Consider naming the Docker image (pdfservice) and the docker container (taxxorpdfservice) the same
#
