pipeline {
    agent any

    options {
        disableConcurrentBuilds()
        timestamps()
        skipDefaultCheckout(true)
    }

    environment {
        COMPOSE_PROJECT_NAME = "vinheria-ci-${BUILD_NUMBER}"
        IMAGE_PREFIX = 'ghcr.io/mats057/agnello-ms'
        IMAGE_TAG = "${BUILD_NUMBER}"
        CI_IMAGE = "vinheria-ci-${BUILD_NUMBER}"
        GATEWAY_PORT = '0'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Preparar config de deployment') {
            steps {
                withCredentials([
                    string(credentialsId: 'vinheria-jwt-secret', variable: 'JWT_SECRET'),
                    string(credentialsId: 'vinheria-rabbitmq-user', variable: 'RABBITMQ_USER'),
                    string(credentialsId: 'vinheria-rabbitmq-password', variable: 'RABBITMQ_PASSWORD')
                ]) {
                    sh '''
                        set +x
                        printf '%s\n' \
                            "JWT_SECRET=${JWT_SECRET}" \
                            'ESTOQUE_URL=http://estoque-service:8080' \
                            'MENSAGERIA_URL=http://mensageria-service:8080' \
                            'RABBITMQ_HOST=rabbitmq' \
                            "RABBITMQ_USER=${RABBITMQ_USER}" \
                            "RABBITMQ_PASSWORD=${RABBITMQ_PASSWORD}" \
                            'ALLOW_SELF_SIGNED_CERT=false' \
                            'CERT_PASSWORD=ci-only' > .env
                    '''
                }
            }
        }

        stage('Validar manifests') {
            steps {
                sh '''
                    docker compose -f docker-compose.yml config --quiet
                    docker compose -f docker-compose.yml -f docker-compose.build.yml config --quiet
                '''
            }
        }

        stage('CI: build e test') {
            steps {
                sh 'docker build --file Dockerfile.ci --tag "$CI_IMAGE" .'
            }
        }

        stage('Construir imagens') {
            steps {
                sh 'docker compose -f docker-compose.yml -f docker-compose.build.yml build'
            }
        }

        stage('Publicar imagens no GHCR') {
            steps {
                withCredentials([
                    usernamePassword(
                        credentialsId: 'github-container-registry',
                        usernameVariable: 'GHCR_USER',
                        passwordVariable: 'GHCR_TOKEN'
                    )
                ]) {
                    sh '''
                        set -eu
                        trap 'docker logout ghcr.io >/dev/null 2>&1 || true' EXIT
                        printf '%s' "$GHCR_TOKEN" | docker login ghcr.io \
                            --username "$GHCR_USER" --password-stdin
                        for service in gateway-service auth-service estoque-service pedidos-service mensageria-service; do
                            docker push "${IMAGE_PREFIX}:${IMAGE_TAG}-${service}"
                        done
                    '''
                }
            }
        }

        stage('Baixar imagens publicadas') {
            steps {
                withCredentials([
                    usernamePassword(
                        credentialsId: 'github-container-registry',
                        usernameVariable: 'GHCR_USER',
                        passwordVariable: 'GHCR_TOKEN'
                    )
                ]) {
                    sh '''
                        set -eu
                        trap 'docker logout ghcr.io >/dev/null 2>&1 || true' EXIT
                        printf '%s' "$GHCR_TOKEN" | docker login ghcr.io \
                            --username "$GHCR_USER" --password-stdin
                        docker compose -f docker-compose.yml pull
                    '''
                }
            }
        }

        stage('CD: deploy') {
            steps {
                sh 'docker compose -f docker-compose.yml up --detach --no-build --remove-orphans'
            }
        }

        stage('CD: smoke test') {
            steps {
                sh '''
                    set -e
                    GATEWAY_CONTAINER="$(docker compose -f docker-compose.yml ps -q gateway-service)"
                    GATEWAY_NETWORK="$(docker inspect "$GATEWAY_CONTAINER" \
                        --format '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}}{{end}}')"

                    if [ -z "$GATEWAY_NETWORK" ]; then
                        echo "Não foi possível identificar a rede do Gateway."
                        docker compose ps
                        exit 1
                    fi

                    for attempt in $(seq 1 30); do
                        if docker run --rm --network "$GATEWAY_NETWORK" curlimages/curl:8.10.1 \
                            --fail --silent http://gateway-service:8080/health; then
                            break
                        fi
                        if [ "$attempt" -eq 30 ]; then
                            echo "Gateway não ficou saudável no tempo esperado."
                            docker compose -f docker-compose.yml logs --no-color
                            exit 1
                        fi
                        sleep 2
                    done

                    docker run --rm --network "$GATEWAY_NETWORK" curlimages/curl:8.10.1 \
                        --fail --silent --show-error \
                        http://gateway-service:8080/catalog > /dev/null
                '''
            }
        }

    }

    post {
        always {
            sh 'docker compose -f docker-compose.yml down --remove-orphans --volumes 2>/dev/null || true'
            sh 'docker image rm "$CI_IMAGE" 2>/dev/null || true'
            sh 'rm -f .env'
        }
    }
}
