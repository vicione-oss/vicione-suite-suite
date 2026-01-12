#!/usr/bin/env bash

#######################################
# Variables
#######################################

# defaults
MODULE='main'
PUBLISH_DIRECTORY='artifacts/publish'
REMOTE_USER='suite'
HOST_IP='159.89.13.71'
ENVIRONMENT='Review'
PLATFORM='linux-x64'
PRESERVE_DATA='false'

#######################################
# Package handling
#######################################

PACKAGES_DIRECTORY=$(dirname $0)
source "${PACKAGES_DIRECTORY}/package_basic.sh"
source "${PACKAGES_DIRECTORY}/package_publish.sh"
source "${PACKAGES_DIRECTORY}/package_settings.sh"
source "${PACKAGES_DIRECTORY}/package_digitalocean.sh"

#######################################
# Argument handling
#######################################

PARAMS=""
while (("$#")); do
  case "${1}" in
  -h | --host)
    if [ -n "${2}" ] && [ ${2:0:1} != "-" ]; then
      HOST_IP="${2}"
      shift 2
    else
      basic::err "Argument for ${1} is missing" >&2
      exit 1
    fi
    ;;
  -e | --environment)
    if [ -n "${2}" ] && [ ${2:0:1} != "-" ]; then
      ENVIRONMENT="${2}"
      shift 2
    else
      basic::err "Argument for ${1} is missing" >&2
      exit 1
    fi
    ;;
  -p | --platform)
    if [ -n "${2}" ] && [ ${2:0:1} != "-" ]; then
      PLATFORM="${2}"
      shift 2
    else
      basic::err "Argument for ${1} is missing" >&2
      exit 1
    fi
    ;;
  -ca | --create-artifacts)
    MODULE='create_artifacts'
    PUBLISH_DIRECTORY='artifacts'
    shift 1
    ;;
  -pd | --preserve-data)
    PRESERVE_DATA='true'
    shift 1
    ;;
  -* | --*=) # unsupported flags
    basic::err "Unsupported flag ${1}" >&2
    exit 1
    ;;
  *) # preserve positional arguments
    PARAMS="${PARAMS} ${1}"
    shift
    ;;
  esac
done

# set positional arguments in their proper place
eval set -- "${PARAMS}"

# calculated variables need to take care of overidden variables from argument handling
mkdir -p "${PUBLISH_DIRECTORY}" # need to be created in order to get the real path
ABSOLUTE_PUBLISH_DIRECTORY=$(realpath "${PUBLISH_DIRECTORY}")
ABSOLUTE_BASE_DIRECTORY=$(realpath "./")
VARIABLES_DIRECTORY=$(dirname $0)
if [[ -f "${VARIABLES_DIRECTORY}/.configuration" ]]; then source "${VARIABLES_DIRECTORY}/.configuration"; fi

#######################################
# Behaviour configuration
#######################################

set -o errexit
set -o nounset
set -o pipefail

#######################################
# Definitions
########################################

prepare_script_environment() {
  # check necessary variables
  basic::log 'checking necessary variables'
  if [[ -z ${HOST_IP+x} ]]; then basic::warn "HOST_IP is unset" && HOST_IP=''; fi

  # install software
  basic::log 'install software'
  apt -o DPkg::Lock::Timeout=600 update -y -qq
  apt -o DPkg::Lock::Timeout=600 install -y -qq \
    openssh-client \
    jq \
    rsync

  # create directories
  basic::log 'create directories'
  mkdir -p "${PUBLISH_DIRECTORY}"

  # prepare SSH
  basic::log 'prepare SSH'
  mkdir -p ~/.ssh &&
    chmod 700 ~/.ssh &&
    touch ~/.ssh/known_hosts &&
    chmod 644 ~/.ssh/known_hosts
  eval $(ssh-agent)
  echo "${SSH_PRIVATE_KEY_DIGITALOCEAN}" | tr -d '\r' | ssh-add - >/dev/null
  ssh-add -L
}

execute_remote() {
  ssh -q -o "UserKnownHostsFile=/dev/null" -o "StrictHostKeyChecking=no" -o "ConnectTimeout=10" ${REMOTE_USER}@${HOST_IP} "${1}"
}

create_self_signed_certificates_for_ip() {
  basic::log 'creating certificates'
  scp -q -o "UserKnownHostsFile=/dev/null" -o "StrictHostKeyChecking=no" -o "ConnectTimeout=10" ca-cert.* ${REMOTE_USER}@${HOST_IP}:~
  execute_remote 'openssl x509 -noout -checkend $((60 * 60 * 24 * 30)) -in cert.crt &> /dev/null || rm cert.* || true'
  execute_remote '([ ! -f cert.csr ] || [ ! -f cert.crt ] || [ ! -f cert.key ] || [ ! -f cert.pfx ] || [ ! -f .certified ]) &&
    rm -f cert.* .certified || true'
  execute_remote '[ ! -f cert.csr ] &&
    openssl req \
    -new \
    -sha256 \
    -keyout cert.key \
    -subj "/C=DE/ST=Saxony/L=Zwickau/O=ifm/OU=Development/CN='${HOST_IP}'/emailAddress=maximilian.raab@ifm.com" \
    -reqexts SAN \
    -config <(cat /etc/ssl/openssl.cnf <(printf "\n[SAN]\nsubjectAltName=IP:'${HOST_IP}'")) \
    -out cert.csr \
    -nodes || true'
  execute_remote '[ ! -f cert.crt ] &&
    openssl x509 \
    -req \
    -extfile <(printf "subjectAltName=IP:'${HOST_IP}'") \
    -days 120 \
    -in cert.csr \
    -CA ca-cert.crt \
    -CAkey ca-cert.key \
    -CAcreateserial \
    -out cert.crt \
    -sha256 || true'
  execute_remote '[ ! -f cert.pfx ] &&
    openssl pkcs12 -export -out cert.pfx -inkey cert.key -in cert.crt -passout pass: || true'
  execute_remote '[ ! -f .certified ] &&
    sudo cp ca-cert.crt ca-cert.key cert.crt cert.key cert.pfx /usr/local/share/ca-certificates/ || true'
  execute_remote '[ ! -f .certified ] &&
    sudo chmod a+r /usr/local/share/ca-certificates/* || true'
  execute_remote '[ ! -f .certified ] &&
    sudo update-ca-certificates && touch .certified || true'
}

download_necessary_docker_images() {
  basic::log 'downloading docker images'
  execute_remote "sudo docker pull dpage/pgadmin4"
  execute_remote 'sudo docker pull postgres'
  execute_remote 'sudo docker pull rabbitmq:3-management'
  execute_remote 'sudo docker pull eclipse-mosquitto'
}

start_necessary_docker_container() {
  basic::log 'starting docker containers'
  ##### nework
  execute_remote '[ ! "$(sudo docker network list | grep postgres-network)" ] && sudo docker network create postgres-network || true'
  ##### pgadmin
  local pgadmin_config_json=$(
    cat <<-END
{
  "Servers": {
    "$CI_COMMIT_REF_SLUG":{
      "Name":"$CI_COMMIT_REF_SLUG",
      "Group":"Review",
      "Port":5432,
      "Username":"postgres",
      "Host":"postgres",
      "SSLMode":"prefer",
      "MaintenanceDB":"postgres"
    }
  }
}
END
  )

  echo "${pgadmin_config_json}" >servers.json
  scp \
    -q \
    -o "UserKnownHostsFile=/dev/null" \
    -o "StrictHostKeyChecking=no" \
    -o "ConnectTimeout=10" \
    servers.json ${REMOTE_USER}@${HOST_IP}:~/servers.json
  execute_remote '[ ! "$(sudo docker ps -a | grep pgadmin)" ] &&
    sudo docker run \
      -d \
      --network postgres-network \
      --restart always \
      --pull always \
      -p 8080:80 \
      -v ~/servers.json:/pgadmin4/servers.json \
      -e "PGADMIN_DEFAULT_EMAIL=admin@admin.de" \
      -e "PGADMIN_DEFAULT_PASSWORD=admin" \
      --name pgadmin \
      dpage/pgadmin4 || true'
  ##### postgres
  execute_remote '[ ! "$(sudo docker ps -a | grep postgres)" ] &&
    sudo docker run \
      -d \
      --network postgres-network \
      --restart always \
      --pull always \
      -p 127.0.0.1:5432:5432 \
      -e POSTGRES_USER=postgres \
      -e POSTGRES_PASSWORD=postgres \
      -e POSTGRES_DB=postgres \
      --name postgres \
      postgres || true'
  ##### rabbitMQ
  local rabbitmq_config_lines=$(
    cat <<-END
default_pass = rabbit
default_user = rabbit
END
  )

  echo "${rabbitmq_config_lines}" >50-rabbitmq.conf
  scp \
    -q \
    -o "UserKnownHostsFile=/dev/null" \
    -o "StrictHostKeyChecking=no" \
    -o "ConnectTimeout=10" \
    50-rabbitmq.conf ${REMOTE_USER}@${HOST_IP}:~/50-rabbitmq.conf
  execute_remote '[ ! "$(sudo docker ps -a | grep rabbitmq)" ] &&
    sudo docker run \
      -d \
      --restart always \
      --pull always \
      -p 8081:15672 \
      -p 5672:5672 \
      -v ~/50-rabbitmq.conf:/etc/rabbitmq/conf.d/50-rabbitmq.conf \
      --hostname review \
      --name rabbitmq \
      rabbitmq:3-management || true'
    #### mosquitto
    local mosquitto_config_lines=$(
      cat <<-END
listener 1883
allow_anonymous false
password_file /mosquitto/config/mosquitto-auth.conf

listener 9001
protocol websockets
certfile /mosquitto/certs/cert.crt
keyfile /mosquitto/certs/cert.key
END
    )
    echo "${mosquitto_config_lines}" >mosquitto.conf
  scp \
    -q \
    -o "UserKnownHostsFile=/dev/null" \
    -o "StrictHostKeyChecking=no" \
    -o "ConnectTimeout=10" \
    mosquitto.conf ${REMOTE_USER}@${HOST_IP}:~/mosquitto.conf
    local mosquitto_auth_config_lines=$(
      cat <<-'END'
mosquitto:$7$101$wPEdeBhIhHTQKLTE$atZ9vuSckHr0iDzP2G+83wf2zF1swx5KUW2OGRjUxLm9PyXMG82/WkGX+/RugTdayexqF38fMUSFbsIrEe2GuQ==
END
    )
    echo "${mosquitto_auth_config_lines}" >mosquitto-auth.conf
  scp \
    -q \
    -o "UserKnownHostsFile=/dev/null" \
    -o "StrictHostKeyChecking=no" \
    -o "ConnectTimeout=10" \
    mosquitto-auth.conf ${REMOTE_USER}@${HOST_IP}:~/mosquitto-auth.conf
  execute_remote '[ ! "$(sudo docker ps -a | grep mosquitto)" ] &&
    sudo docker run \
      -d \
      --restart always \
      --pull always \
      -p 1883:1883 \
      -p 9001:9001 \
      -v ~/mosquitto-auth.conf:/mosquitto/config/mosquitto-auth.conf:ro \
      -v ~/mosquitto.conf:/mosquitto/config/mosquitto.conf:ro \
      -v /usr/local/share/ca-certificates:/mosquitto/certs:ro \
      --name mosquitto \
      eclipse-mosquitto || true'
}

deploy_to_target_machine() {
  basic::log 'stopping old application'
  execute_remote 'sudo pkill -f ViciOne.Suite' || true
  basic::log "PRESERVE_DATA is set to: ${PRESERVE_DATA}"
  if [[ "${PRESERVE_DATA}" == 'true' ]]; then
    execute_remote 'sudo rm -rf /tmp/AppData'
    execute_remote 'sudo cp -r /opt/app/AppData/ /tmp/AppData/' || true
  else
    # if we don't want to preserve the data, we have to kill the other processes as well
    execute_remote 'sudo pkill -f ViciOne' || true
  fi
  execute_remote 'sudo find /tmp -type d -name "app-*" | sort -r | tail -n +2 | xargs -I {} sudo rm -rf {} || true'
  execute_remote 'sudo mv /opt/app/ /tmp/app-$(date +"%s")/ || true'
  execute_remote 'sudo mkdir -p /opt/app/ && sudo chown -R suite:suite /opt/app/'
  basic::log "copying application to server"
  rsync \
    -e 'ssh -q -o "UserKnownHostsFile=/dev/null" -o "StrictHostKeyChecking=no" -o "ConnectTimeout=10"' \
    -rz \
    --inplace \
    --stats \
    --delete \
    "${ABSOLUTE_PUBLISH_DIRECTORY}"/ ${REMOTE_USER}@${HOST_IP}:/opt/app
  if [[ "${PRESERVE_DATA}" == 'true' ]]; then
    execute_remote 'sudo rm -rf /opt/app/AppData'
    execute_remote 'sudo mv /tmp/AppData /opt/app/AppData && sudo chown -R suite:suite /opt/app/' || true
  fi
  settings::echo_suite_start_script "${ENVIRONMENT}" "${HOST_IP}" >start.sh
  scp \
    -q \
    -o "UserKnownHostsFile=/dev/null" \
    -o "StrictHostKeyChecking=no" \
    -o "ConnectTimeout=10" \
    start.sh ${REMOTE_USER}@${HOST_IP}:~/start.sh
  basic::log 'starting application'
  execute_remote 'cd /opt/app/ && sudo setcap cap_net_bind_service=ep ViciOne.Suite.Core.OS'
  execute_remote 'chmod +x start.sh'
  execute_remote './start.sh'
}

check_application_status() {
  basic::log 'waiting for the application to become available'
  sleep 3
  basic::log 'checking application state'
  if ! execute_remote 'sudo pgrep "ViciOne.Suite"'; then
    execute_remote 'sudo cat /opt/app/suite.log && true'
    return 1
  fi
}

create_artifacts() {
  publish::core_os "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish suite.core.os'

  publish::uihost_blazor_server "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish uihost blazor.server'

  publish::modules "${ABSOLUTE_BASE_DIRECTORY}" "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" "${ENVIRONMENT}" || basic::return 'failed to publish the modules'

  settings::echo_suite_start_script "${ENVIRONMENT}" "##ip##" >start.sh
}

prepare_app() {
  publish::core_os "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish suite.core.os'

  publish::uihost_blazor_server "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish uihost blazor.server'

  publish::modules "${ABSOLUTE_BASE_DIRECTORY}" "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" "${ENVIRONMENT}" || basic::return 'failed to publish the modules'
}

install_necessary_software() {
  basic::log 'install necessary software on remote machine'
  # wait for update to finish potential running activities
  # this is still necessary, because DPkg::Lock::Timeout doens't apply to the update operation, unfortunately
  execute_remote 'while sudo fuser /var/{lib/{dpkg,apt/lists},cache/apt/archives}/lock >/dev/null 2>&1; do sleep 1; done'
  execute_remote 'sudo apt -o DPkg::Lock::Timeout=600 update -y -qq &&
   sudo apt -o DPkg::Lock::Timeout=600 install -y -qq libgdiplus libc6-dev'
}

setup_host() {
  create_self_signed_certificates_for_ip || basic::return 'failed to create self signed certificates'
  start_necessary_docker_container || basic::return 'failed to start docker containers'
  install_necessary_software || basic::return 'failed to install necessary software'
}

#######################################
# Modules
#######################################

main() {
  local pids

  prepare_script_environment || basic::return 'could not setup script environment'

  (setup_host || basic::return 'faild to setup the host') &
  pids[0]=$!

  (prepare_app || basic::return 'failed to prepare the application') &
  pids[1]=$!

  for pid in ${pids[*]}; do
    wait $pid
  done

  deploy_to_target_machine || basic::err 'failed to deploy to the remote host'
  check_application_status || basic::return 'application is not running'
}

eval "${MODULE}"
