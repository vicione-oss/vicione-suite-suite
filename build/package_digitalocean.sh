#!/usr/bin/env bash

#######################################
# Variables
#######################################

# defaults
readonly DIGITALOCEAN_BASE_URL="https://api.digitalocean.com/v2"

#######################################
# Definitions
#######################################

digitalocean::check_ip() {
  local ip="${1}"

  basic::log "Checking ip address '${ip}'"

  if ssh -q -o "UserKnownHostsFile=/dev/null" -o "StrictHostKeyChecking=no" -o "ConnectTimeout=10" root@${ip} 'echo pong from deployment' &>/dev/null; then
    basic::log "... Host is reachable"
    return 0
  fi

  basic::warn "... Host is not reachable"
  return 1
}

digitalocean::get_deployment() {
  local filter_tag=${1-Env:$CI_ENVIRONMENT_SLUG}
  local file=$(mktemp)
  local response=$(curl \
    -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
    --silent \
    --connect-timeout 10 \
    -w "%{http_code}" \
    -o ${file} \
    "${DIGITALOCEAN_BASE_URL}/droplets?tag_name=ci-vosuite-review")
  local body=$(jq . ${file} <<<cat)

  if [[ $response -ne 200 ]]; then
    basic::err "Could not get droplets - Response was: ${response}\n${body}"
  else
    jq -c '.droplets[]' "${file}" | while read droplet; do

      if echo ${droplet} | jq -e ".tags|any(. == \"${filter_tag}\")" &>/dev/null; then
        existing_droplet_ip=$(echo ${droplet} | jq -r '.networks.v4 | map(select(.type=="public"))[0].ip_address')
        echo $existing_droplet_ip
        rm -rf "${file}"
        exit 0
      fi

    done
  fi

  rm -rf "${file}"
}

digitalocean::setup_droplet() {
  local file=$(mktemp)
  local timestamp=$(date '+%Y%m%d%H%M%S')
  local droplet_name="${1-default}"
  if [[ "${droplet_name}" == "default" ]]; then local droplet_name="vicione-suite-${timestamp}"; fi
  local base_image="${2-default}"
  if [[ "${base_image}" == "default" ]]; then local base_image="vicione-suite-base-image"; fi

  local response=$(curl \
    -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
    --silent \
    --connect-timeout 10 \
    -w "%{http_code}" \
    -o ${file} \
    "${DIGITALOCEAN_BASE_URL}/account/keys")
  local body=$(jq . ${file} <<<cat)
  
  if [[ $response -eq 401 ]]; then
    basic::return "Could not authenticate with API - Response was: ${response}\n${body}"
    exit 1
  fi

  if [[ $response -ne 200 ]]; then
    basic::return "Could not get ssh keys - Response was: ${response}\n${body}"
  else
    local ssh_key_id=$(jq -r '.ssh_keys | map(select(.name=="vosuite-deploy-key"))[0].id' ${file} <<<cat)
  fi

  local response=$(curl \
    -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
    --silent \
    --connect-timeout 10 \
    -w "%{http_code}" \
    -o ${file} \
    "${DIGITALOCEAN_BASE_URL}/snapshots?page=1&per_page=100")
  local body=$(jq . ${file} <<<cat)
  if [[ $response -ne 200 ]]; then
    basic::return "Could not get ssh keys - Response was: ${response}\n${body}"
  else
    local image_id=$(jq -r '.snapshots | map(select(.name | contains ("'${base_image}'"))) | sort_by(.created_at) | reverse[0].id' ${file} <<<cat)
    if [[ ${image_id} == "null" ]]; then local image_id="ubuntu-24-04-x64"; fi
  fi

  local json=$(
    cat <<-END
{
  "name": "${droplet_name}",
  "region": "fra1",
  "size": "s-1vcpu-2gb",
  "image": "${image_id}",
  "ssh_keys": [$ssh_key_id],
  "backups": false,
  "ipv6": false,
  "user_data": null,
  "private_networking": null,
  "volumes": null,
  "tags": ["ci-vosuite-review", "Env:${CI_ENVIRONMENT_SLUG--}", "MR:${CI_MERGE_REQUEST_IID--}", "Pipeline:${CI_PIPELINE_ID--}", "Job:${CI_JOB_ID--}", "Branch:${CI_MERGE_REQUEST_SOURCE_BRANCH_NAME--}", "Name:${droplet_name}"]
}
END
  )
  local parsedJson=$(jq . <<<${json})

  basic::log "Creating new Droplet '${droplet_name}' with:\n${parsedJson}"

  local response=$(
    curl \
      -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
      --silent \
      --connect-timeout 10 \
      -w "%{http_code}" \
      -o ${file} \
      -H "Content-Type: application/json" \
      -d "${json}" \
      "$DIGITALOCEAN_BASE_URL/droplets"
  )
  local body=$(jq . ${file} <<<cat)
  if [[ $response -ne 202 ]]; then
    basic::err "Could not create droplet - Response was: ${response}\n${body}"
  fi

  local status=$(jq -r '.droplet.status' <<<${body})
  basic::log "Status: ${status}"
  if [[ "${status}" != 'new' ]]; then
    basic::return "Could not verify droplet state:\n${body}"
  fi

  local id=$(jq -r '.droplet.id' <<<${body})
  basic::log "Droplet with ID '${id}' created"

  basic::log "Waiting for droplet to boot"
  for i in {1..60}; do
    local response=$(
      curl \
        -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
        --silent \
        --connect-timeout 10 \
        -w "%{http_code}" \
        -o ${file} \
        "${DIGITALOCEAN_BASE_URL}/droplets/${id}"
    )
    local body=$(jq . ${file} <<<cat)

    if [[ $response -ne 200 ]]; then
      basic::err "Could not get droplet state - Response was: ${response}\n${body}"
    fi

    local status=$(jq -r '.droplet.status' <<<${body})
    [[ "${status}" == 'active' ]] && break
    sleep 10
  done

  if [[ "${status}" != 'active' ]]; then
    basic::return "Droplet took to long to boot - Status: ${DROPLET_STATUS}"
  fi

  local ip=$(jq -r '.droplet.networks.v4 | map(select(.type=="public"))[0].ip_address' <<<${body})

  until digitalocean::check_ip "${ip}"; do
    sleep 2
  done

  basic::log "*****************************"
  basic::log "* Droplet is ready to use"
  basic::log "* IP address: ${ip}"
  basic::log "*****************************"

  rm -rf "${file}"
}

digitalocean::analyze_droplet() {
  local droplet_id="${1}"
  local file=$(mktemp)

  basic::log "Analyzing droplet '${droplet_id}'"

  local response=$(curl \
    -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
    --silent \
    --connect-timeout 10 \
    -w "%{http_code}" \
    -o ${file} \
    "${DIGITALOCEAN_BASE_URL}/droplets/${droplet_id}")
  local body=$(jq . ${file} <<<cat)

  if [[ $response -ne 200 ]]; then
    basic::err "Could not get droplet information '${droplet_id}' - Response was: ${response}\n${body}"
  else
    local ip=$(jq -r '.droplet.networks.v4 | map(select(.type=="public"))[0].ip_address' ${file} <<<cat)
    local last_deployment=$(ssh -q -o "StrictHostKeyChecking=no" -o ConnectTimeout=60 -o ConnectionAttempts=1 root@${ip} 'cat /home/suite/.deployed 2> /dev/null' || date -d "now - 30 years" +%s)
    local one_day_ago=$(date -d 'now - 1 days' +%s)
    local ten_years_ago=$(date -d 'now - 10 years' +%s)
    local twenty_minutes_ago=$(date -d 'now - 20 mins' +%s)

    basic::log "... IP: ${ip}"

    # does a deployment exist?
    if ((last_deployment >= ten_years_ago)); then
      basic::log "... deployment was last updated at $(date -ud @${last_deployment})"
      # is the deployment older than 1 day?
      if ((last_deployment <= one_day_ago)); then
        basic::log '... Removing droplet because it is older than 1 day'
        digitalocean::remove_machine "${droplet_id}"
      fi
    # .. no does not exists
    else
      basic::log '... no deployment exists'
      # is the droplet for more than 1 hour stale?
      local created_at_raw=$(jq -r '.droplet.created_at' ${file} <<<cat)
      local created_at=$(date -d "${created_at_raw}" +%s)
      basic::log "... droplet created at $(date -ud @${created_at})"
      if ((created_at <= twenty_minutes_ago)); then
        basic::log '... removing droplet because no deployment exists'
        digitalocean::remove_machine "${droplet_id}"
      fi
    fi
  fi

  rm -rf "${file}"
}

digitalocean::cleanup_orphanded_instances() {
  local file=$(mktemp)
  local response=$(curl \
    -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
    --silent \
    --connect-timeout 10 \
    -w "%{http_code}" \
    -o ${file} \
    "${DIGITALOCEAN_BASE_URL}/droplets?tag_name=ci-vosuite-review")
  local body=$(jq . ${file} <<<cat)

  if [[ $response -ne 200 ]]; then
    basic::err "Could not get droplets - Response was: ${response}\n${body}"
  else
    local droplets=$(jq ".droplets[].id" ${file} <<<cat)

    if [[ ! -z "${droplets}" ]]; then
      for droplet_id in $droplets; do
        digitalocean::analyze_droplet "${droplet_id}"
      done
    fi
  fi

  rm -rf "${file}"
}

digitalocean::remove_machine() {
  local file=$(mktemp)
  local droplet_id_to_remove="${1}"
  basic::log "Removing droplet '${droplet_id_to_remove}'"

  local response=$(curl \
    -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
    -X DELETE \
    --silent \
    --connect-timeout 10 \
    -w "%{http_code}" \
    -o ${file} \
    "${DIGITALOCEAN_BASE_URL}/droplets/${droplet_id_to_remove}")
  local body=$(jq . ${file} <<<cat)
  if [[ $response -ne 204 ]]; then
    basic::err "Could not remove droplet - Response was: ${response}\n${body}"
  else
    basic::log "... done"
  fi

  rm -rf "${file}"
}

digitalocean::remove_machine_by_tag() {
  local file=$(mktemp)
  local response=$(curl \
    -H "Authorization: Bearer ${DIGOCEAN_KEY}" \
    --silent \
    --connect-timeout 10 \
    -w "%{http_code}" \
    -o ${file} \
    "${DIGITALOCEAN_BASE_URL}/droplets?tag_name=ci-vosuite-review")
  local body=$(jq . ${file} <<<cat)

  if [[ $response -ne 200 ]]; then
    basic::err "Could not get droplets - Response was: ${response}\n${body}"
  else
    jq -c '.droplets[]' "${file}" | while read droplet; do

      if echo ${droplet} | jq -e ".tags|any(. == \"Env:${CI_ENVIRONMENT_SLUG}\")" &>/dev/null; then
        local existing_droplet_id=$(echo ${droplet} | jq '.id')
        digitalocean::remove_machine "${existing_droplet_id}"
      fi

    done
  fi

  rm -rf "${file}"
}
