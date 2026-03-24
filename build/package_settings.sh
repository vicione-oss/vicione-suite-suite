#!/usr/bin/env bash

#######################################
# Package handling
#######################################

PACKAGES_DIRECTORY=$(dirname $0)
source "${PACKAGES_DIRECTORY}/package_basic.sh"

#######################################
# Definitions
#######################################

settings::set_core_os_settings() {
  local ENVIRONMENT_PARAM="${1}"
  local HOST_IP_PARAM="${2}"

  cat <<- EOF
ASPNETCORE_ENVIRONMENT='${ENVIRONMENT_PARAM}'
Kestrel__Endpoints__Http__Url='http://0.0.0.0:80'
Kestrel__Endpoints__Https__Url='https://0.0.0.0:443'
Kestrel__Endpoints__Https__Certificate__Path='/usr/local/share/ca-certificates/cert.pfx'
Kestrel__Endpoints__Https__Certificate__Password=''
Kestrel__Endpoints__Https__Certificate__AllowInvalid='true'
ArtifactRepository__Sources__0__UserName='${MODULE_API_USER}'
ArtifactRepository__Sources__0__TokenEndpoint='https://system.update.ifm/artifactory/vicione-token/token.json'
ArtifactRepository__Sources__0__Endpoint='https://system.update.ifm/artifactory/vicione-suite'
EOF

  if [[ "${ENVIRONMENT_PARAM^^}" == "STANDALONE" ]]; then
    cat <<- EOF
DetailedErrors='true'
Instance__Type='Standalone'
HostManagement__MockClient__DataSource='SystemConfigurationEmbedded'
MessageBus__UseInMemoryBus='true'
ModuleLoader__AllowPreReleases='true'
ModuleLoader__ModulesPath='Modules'
ModuleLoader__DumpMappingFilePath='/opt/app/AppData/suite-mapping.log'
ModuleLoader__ManifestSeedPath='/home/suite/initial-modules.json'
ArtifactRepository__Sources__1__UserName='${MODULE_API_USER}'
ArtifactRepository__Sources__1__TokenEndpoint='https://system.update.ifm/artifactory/vicione-token/token.json'
ArtifactRepository__Sources__1__Endpoint='https://system.update.ifm/artifactory/vicione-suite-staging'
ArtifactRepository__Sources__2__UserName='${MODULE_API_USER}'
ArtifactRepository__Sources__2__TokenEndpoint='https://system.update.ifm/artifactory/vicione-token/token.json'
ArtifactRepository__Sources__2__Endpoint='https://system.update.ifm/artifactory/vicione-suite-dev'
MqttClient__WebSocketClient__Endpoint='wss://${HOST_IP_PARAM}/mqtt'
MqttClient__WebSocketClient__UserName='${MQTT_USER}'
MqttClient__WebSocketClient__Password='${MQTT_PASSWORD}'
MqttClient__WebSocketClient__TopicFilter='data'
MqttClient__ServiceClient__Endpoint='${HOST_IP_PARAM}'
MqttClient__ServiceClient__UserName='${MQTT_USER}'
MqttClient__ServiceClient__Password='${MQTT_PASSWORD}'
UserManagement__SeedTestUsers='true'
EOF
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "MINIMAL" ]]; then
    cat <<- EOF
DetailedErrors='true'
Logging__LogTargets__0='Journal'
Instance__Type='Standalone'
MessageBus__UseInMemoryBus='true'
ModuleLoader__ModulesPath='Modules'
MqttClient__WebSocketClient__Endpoint='wss://${HOST_IP_PARAM}/mqtt'
MqttClient__WebSocketClient__UserName='${MQTT_USER}'
MqttClient__WebSocketClient__Password='${MQTT_PASSWORD}'
MqttClient__WebSocketClient__TopicFilter='data'
MqttClient__ServiceClient__Endpoint='${HOST_IP_PARAM}'
MqttClient__ServiceClient__UserName='${MQTT_USER}'
MqttClient__ServiceClient__Password='${MQTT_PASSWORD}'
ViciOneSuiteClusterManagement__HideClusterEditorNavTile='true'
EOF
    # The 'MINIMAL' deployment sets a base configuration for the debian package and should not contain any modules.
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "MASTER" ]]; then
    cat <<- EOF
ConnectionStrings__Postgres='Server=127.0.0.1;Port=5432;Database=vo-suite;User Id=postgres;Password=postgres;'
Instance__Type='Master'
MessageBus__Connection__Host='localhost'
MessageBus__Connection__Port='5672'
MessageBus__Connection__ManagementPort='15672'
MessageBus__Connection__User='rabbit'
MessageBus__Connection__Pass='rabbit'
ModuleLoader__AllowPreReleases='true'
ModuleLoader__ModulesPath='Modules'
ModuleLoader__ManifestSeedPath='/home/suite/initial-modules.json'
MqttClient__WebSocketClient__Endpoint='wss://159.89.13.71/'
MqttClient__WebSocketClient__Port='9001'
MqttClient__WebSocketClient__UserName='${MQTT_USER}'
MqttClient__WebSocketClient__Password='${MQTT_PASSWORD}'
MqttClient__ServiceClient__Endpoint='159.89.13.71'
MqttClient__ServiceClient__Port='1883'
MqttClient__ServiceClient__UserName='${MQTT_USER}'
MqttClient__ServiceClient__Password='${MQTT_PASSWORD}'
UserManagement__SeedTestUsers='true'
EOF
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "REVIEW" ]]; then
    cat <<- EOF
ConnectionStrings__Postgres='Server=127.0.0.1;Port=5432;Database=vo-suite;User Id=postgres;Password=postgres;'
Instance__Type='Master'
MessageBus__Connection__Host='localhost'
MessageBus__Connection__Port='5672'
MessageBus__Connection__ManagementPort='15672'
MessageBus__Connection__User='rabbit'
MessageBus__Connection__Pass='rabbit'
ModuleLoader__ModulesPath='Modules'
ModuleLoader__ManifestSeedPath='/home/suite/initial-modules.json'
MqttClient__WebSocketClient__Endpoint='wss://159.89.13.71/'
MqttClient__WebSocketClient__Port='9001'
MqttClient__WebSocketClient__UserName='${MQTT_USER}'
MqttClient__WebSocketClient__Password='${MQTT_PASSWORD}'
MqttClient__ServiceClient__Endpoint='159.89.13.71'
MqttClient__ServiceClient__Port='1883'
MqttClient__ServiceClient__UserName='${MQTT_USER}'
MqttClient__ServiceClient__Password='${MQTT_PASSWORD}'
UserManagement__SeedTestUsers='true'
EOF
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "SLAVE" ]]; then
    cat <<- EOF
Instance__Type='Slave'
MessageBus__CleanVirtualHost='false'
MessageBus__Connection__Host='159.89.13.71'
MessageBus__Connection__Port='5672'
MessageBus__Connection__ManagementPort='15672'
MessageBus__Connection__User='rabbit'
MessageBus__Connection__Pass='rabbit'
ModuleLoader__AllowPreReleases='true'
ModuleLoader__ModulesPath='Modules'
ModuleLoader__ManifestSeedPath='/home/suite/initial-modules.json'
MqttClient__WebSocketClient__Endpoint='wss://159.89.13.71/'
MqttClient__WebSocketClient__Port='9001'
MqttClient__WebSocketClient__UserName='${MQTT_USER}'
MqttClient__WebSocketClient__Password='${MQTT_PASSWORD}'
MqttClient__ServiceClient__Endpoint='159.89.13.71'
MqttClient__ServiceClient__Port='1883'
MqttClient__ServiceClient__UserName='${MQTT_USER}'
MqttClient__ServiceClient__Password='${MQTT_PASSWORD}'
UserManagement__SeedTestUsers='true'
EOF
  fi
}

settings::set_initial_module_seed()
{
  local ENVIRONMENT_PARAM="${1}"

  if [[ "${ENVIRONMENT_PARAM^^}" == "STANDALONE" ]]; then
    printf "cat <<- EOF > /home/suite/initial-modules.json\n%s\nEOF\n" "$(cat ./deployments/initial-modules.basic.json)"
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "MINIMAL" ]]; then
    # this file get's written but it is not set as env variable, to also test the build in empty manifest
    printf "cat <<- EOF > /home/suite/initial-modules.json\n%s\nEOF\n" "$(cat ./deployments/initial-modules.empty.json)"
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "MASTER" ]]; then
    printf "cat <<- EOF > /home/suite/initial-modules.json\n%s\nEOF\n" "$(cat ./deployments/initial-modules.basic.json)"
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "REVIEW" ]]; then
    printf "cat <<- EOF > /home/suite/initial-modules.json\n%s\nEOF\n" "$(cat ./deployments/initial-modules.basic.json)"
  fi

  if [[ "${ENVIRONMENT_PARAM^^}" == "SLAVE" ]]; then
    printf "cat <<- EOF > /home/suite/initial-modules.json\n%s\nEOF\n" "$(cat ./deployments/initial-modules.basic.json)"
  fi
}

settings::echo_suite_start_script()
{
  local ENVIRONMENT_PARAM="${1}"
  local HOST_IP_PARAM="${2}"
  local SUITE_VARS=$(settings::set_core_os_settings "${ENVIRONMENT_PARAM}" "${HOST_IP_PARAM}" | tr '\n' ' ')
  local SUITE_MODULE_SEED=$(settings::set_initial_module_seed "${ENVIRONMENT_PARAM}")

  echo "#!/usr/bin/env bash"
  echo "${SUITE_MODULE_SEED}"
  echo "sudo pgrep 'ViciOne.Suite' &> /dev/null && exit 0; cd /opt/app/; ${SUITE_VARS} nohup bash -c './ViciOne.Suite.Core.OS && sleep 15 && /home/suite/start.sh' &>/opt/app/suite.log &"
}
