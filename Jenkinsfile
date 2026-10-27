pipeline {
    agent any

    options {
        disableConcurrentBuilds()
        timestamps()
        skipDefaultCheckout(true)
    }

    environment {
        COMPOSE_PROJECT_NAME = 'vinheria'
        COMPOSE_FILE = 'docker-compose.yml'
        CI_IMAGE = 'vinheria-ci:${BUILD_NUMBER}'
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
                sh 'docker compose config --quiet'
            }
        }

        stage('CI: build e test') {
            steps {
                sh 'docker build --file Dockerfile.ci --tag "$CI_IMAGE" .'
            }
        }

        stage('CD: deploy') {
            steps {
                sh 'docker compose up --detach --build --remove-orphans'
            }
        }

        stage('CD: smoke test') {
            steps {
                sh '''
                    set -e
                    for attempt in $(seq 1 30); do
                        if docker run --rm --network vinheria-network curlimages/curl:8.10.1 \
                            --fail --silent http://gateway-service:8080/health; then
                            break
                        fi
                        if [ "$attempt" -eq 30 ]; then
                            echo "Gateway não ficou saudável no tempo esperado."
                            docker compose logs --no-color
                            exit 1
                        fi
                        sleep 2
                    done

                    docker run --rm --network vinheria-network curlimages/curl:8.10.1 \
                        --fail --silent --show-error \
                        http://gateway-service:8080/catalog > /dev/null
                '''
            }
        }
    }

    post {
        always {
            sh 'docker image rm "$CI_IMAGE" 2>/dev/null || true'
            sh 'rm -f .env'
            archiveArtifacts artifacts: 'docker-compose*.yml,docs/**', allowEmptyArchive: false
        }
    }
}
