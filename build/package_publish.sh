#!/usr/bin/env bash

#######################################
# Package handling
#######################################

PACKAGES_DIRECTORY=$(dirname $0)
source "${PACKAGES_DIRECTORY}/package_basic.sh"

#######################################
# Definitions
#######################################

MODULE_BLAZOR_SERVER="Blazor.Server"
MODULE_JITCHAT="Ping"

publish::core_deployment() {
  local PUBLISH_PLATFORM="${1}"

  local CORE_SRC=$(realpath "${PACKAGES_DIRECTORY}/../src/Core.Deployment")

  basic::log "prepare suite.core.deployment using src ${CORE_SRC}"

  if dotnet build "${CORE_SRC}" -c Release -r "${PUBLISH_PLATFORM}" --nologo -v minimal &>/tmp/prepare.log; then
    basic::log "prepared suite.core.deployment architecture: ${PUBLISH_PLATFORM}"
  else
    basic::err "prepared suite.core.deployment architecture: ${PUBLISH_PLATFORM}"
    cat /tmp/prepare.log
  fi
}

publish::core_os() {
  local PUBLISH_FOLDER="${1}"
  local PUBLISH_PLATFORM="${2}"

  local CORE_SRC=$(realpath "${PACKAGES_DIRECTORY}/../src/Core.OS")

  basic::log "publishing suite.core.os using src ${CORE_SRC}"

  if dotnet publish "${CORE_SRC}" -c Release -r "${PUBLISH_PLATFORM}" -o "${PUBLISH_FOLDER}" --self-contained --nologo -v minimal &>/tmp/publish.log; then
    basic::log "published suite.core.os architecture: ${PUBLISH_PLATFORM} to ${PUBLISH_FOLDER}"
  else
    basic::err "publishing suite.core.os architecture: ${PUBLISH_PLATFORM} to ${PUBLISH_FOLDER} failed"
    cat /tmp/publish.log
    exit 1
  fi
}

publish::uihost_blazor_server() {
  local PUBLISH_FOLDER="${1}"
  local PUBLISH_PLATFORM="${2}"

  local UIHOST_SRC=$(realpath "${PACKAGES_DIRECTORY}/../src/${MODULE_BLAZOR_SERVER}.Backend")
  local UIHOST_TRG="${PUBLISH_FOLDER}/UiHosts/${MODULE_BLAZOR_SERVER}"

  basic::log "publishing ${MODULE_BLAZOR_SERVER} using src ${UIHOST_SRC}"

  if dotnet publish "${UIHOST_SRC}" -c Release -r "${PUBLISH_PLATFORM}" -o "${UIHOST_TRG}" --no-self-contained --nologo -v minimal -p CompressionEnabled=false &>/tmp/publish.log; then
    basic::log "published ${MODULE_BLAZOR_SERVER} architecture: ${PUBLISH_PLATFORM} to ${UIHOST_TRG}"
  else
    basic::err "publishing ${MODULE_BLAZOR_SERVER} architecture: ${PUBLISH_PLATFORM} to ${UIHOST_TRG} failed"
    cat /tmp/publish.log
    exit 1
  fi

  # todo - we could remove all files from UIHOST_TRG that are contained in core.os
}

publish::ps_module_by_name() {
  local SCRIPT_SRC_PATH="${1}"
  local MODULE_SRC_PATH="${2}"
  local MODULES_PATH="${3}"
  local MODULE_NAME="${4}"
  local MODULE_PLATFORM="${5}"

  local PUBLISH_SCRIPT=$(realpath "${SCRIPT_SRC_PATH}/publish-module.sh")
  local CLEANUP_SCRIPT=$(realpath "${SCRIPT_SRC_PATH}/cleanup-module.sh")
  local MODULE_TARGET_PATH="${MODULES_PATH}/ViciOne.Suite.${MODULE_NAME}"

  basic::log "publish ${MODULE_NAME} to ${MODULE_TARGET_PATH}"

  # build module platform specific
  bash "${PUBLISH_SCRIPT}" "${MODULE_NAME}" "${MODULE_SRC_PATH}" "${MODULE_PLATFORM}" "${MODULE_TARGET_PATH}"

  # cleanup module
  bash "${CLEANUP_SCRIPT}" "${MODULE_TARGET_PATH}"
}

publish::modules() {
  local MODULE_BASE_DIR="${1}"
  local MODULE_TRG_DIR="${2}/Modules"
  local UIHOST_TRG_DIR="${2}/UiHosts"
  local MODULE_PLATFORM="${3}"
  local ENVIRONMENT_PARAM="${4}"

  local SCRIPT_SRC="${MODULE_BASE_DIR}/shared/deploy"
  local MODULE_SRC="${MODULE_BASE_DIR}/src"
  local MODULE_SAMPLES="${MODULE_BASE_DIR}/samples"

  mkdir -p "${MODULE_TRG_DIR}"

  if [[ "${ENVIRONMENT_PARAM^^}" == "DEVELOPMENT" ]]; then
    # this builds the core.deployment project to ensure the scripts to publish/cleanup modules is ready
    publish::core_deployment "${MODULE_PLATFORM}" || basic::return 'failed to prepare core_deployment'

    publish::ps_module_by_name "${SCRIPT_SRC}" "${MODULE_SAMPLES}" "${MODULE_TRG_DIR}" "${MODULE_JITCHAT}" "${MODULE_PLATFORM}"
  fi  
}
