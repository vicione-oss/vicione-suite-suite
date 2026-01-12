#!/usr/bin/env bash

#######################################
# Variables
#######################################

# defaults
MODULE='main'
PUBLISH_DIRECTORY='./publish-linux'
PLATFORM='linux-x64'

#######################################
# Package handling
#######################################

PACKAGES_DIRECTORY=$(dirname $0)
source "${PACKAGES_DIRECTORY}/package_basic.sh"
source "${PACKAGES_DIRECTORY}/package_publish.sh"

# calculated
mkdir -p "${PUBLISH_DIRECTORY}" # need to be created in order to get the real path
ABSOLUTE_PUBLISH_DIRECTORY=$(realpath "${PUBLISH_DIRECTORY}")
ABSOLUTE_SRC_DIRECTORY=$(realpath "./src")
ABSOLUTE_SAMPLES_DIRECTORY=$(realpath "./samples")
VARIABLES_DIRECTORY=$(dirname $0)
if [[ -f "${VARIABLES_DIRECTORY}/.configuration" ]]; then source "${VARIABLES_DIRECTORY}/.configuration"; fi

#######################################
# Modules
#######################################

main() {
  publish::modules "${ABSOLUTE_SRC_DIRECTORY}" "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish the modules'

  publish::modules "${ABSOLUTE_SAMPLES_DIRECTORY}" "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish the sample modules'
}

eval "${MODULE}"
