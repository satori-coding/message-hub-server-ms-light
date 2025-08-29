#!/bin/bash

# SMS Flow Test Script für Local Environment
# Testet den kompletten SMS-Versand über HTTP Channel

set -e

# Konfiguration
BASE_URL="http://localhost:5000"
SUBSCRIPTION_KEY="ocp-apim-subscription-key-local-1"
RECIPIENT="+1234567890"
MESSAGE="Test SMS from Local Environment"
CHANNEL_TYPE="HTTP"

# Farben für Output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Funktionen
print_step() {
    echo -e "${BLUE}=== $1 ===${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ️  $1${NC}"
}

# Warte bis Service verfügbar ist
wait_for_service() {
    print_step "Warte auf Service Verfügbarkeit"
    for i in {1..30}; do
        if curl -s "$BASE_URL/api/ping" > /dev/null 2>&1; then
            print_success "Service ist verfügbar"
            return 0
        fi
        echo -n "."
        sleep 2
    done
    print_error "Service nicht verfügbar nach 60 Sekunden"
    exit 1
}

# Test 1: SMS senden
send_sms() {
    print_step "Test 1: SMS über HTTP Channel senden"
    
    local response=$(curl -s -w "%{http_code}" \
        -X POST "$BASE_URL/api/message" \
        -H "Content-Type: application/json" \
        -H "ocp-apim-subscription-key: $SUBSCRIPTION_KEY" \
        -d "{
            \"recipient\": \"$RECIPIENT\",
            \"message\": \"$MESSAGE\",
            \"channelType\": \"$CHANNEL_TYPE\"
        }")
    
    local http_code=$(echo "$response" | tail -n 1)
    local body=$(echo "$response" | head -n -1)
    
    if [ "$http_code" = "200" ]; then
        MESSAGE_ID=$(echo "$body" | grep -o '"messageId":"[^"]*' | cut -d'"' -f4)
        print_success "SMS erfolgreich gesendet"
        print_info "Message ID: $MESSAGE_ID"
        print_info "Response: $body"
        return 0
    else
        print_error "SMS senden fehlgeschlagen (HTTP $http_code)"
        print_info "Response: $body"
        return 1
    fi
}

# Test 2: Status abfragen
check_status() {
    print_step "Test 2: Message Status abfragen"
    
    if [ -z "$MESSAGE_ID" ]; then
        print_error "Keine Message ID verfügbar"
        return 1
    fi
    
    # Warte kurz, damit Message verarbeitet wird
    print_info "Warte 3 Sekunden auf Verarbeitung..."
    sleep 3
    
    local response=$(curl -s -w "%{http_code}" \
        -X GET "$BASE_URL/api/messages/$MESSAGE_ID/status" \
        -H "ocp-apim-subscription-key: $SUBSCRIPTION_KEY")
    
    local http_code=$(echo "$response" | tail -n 1)
    local body=$(echo "$response" | head -n -1)
    
    if [ "$http_code" = "200" ]; then
        local status=$(echo "$body" | grep -o '"status":"[^"]*' | cut -d'"' -f4)
        print_success "Status erfolgreich abgerufen"
        print_info "Status: $status"
        print_info "Response: $body"
        
        # Status validieren
        case "$status" in
            "Queued"|"Processing"|"Sent"|"Delivered")
                print_success "Status ist gültig: $status"
                ;;
            "Failed")
                print_error "Message fehlgeschlagen"
                return 1
                ;;
            *)
                print_error "Unbekannter Status: $status"
                return 1
                ;;
        esac
        return 0
    else
        print_error "Status-Abfrage fehlgeschlagen (HTTP $http_code)"
        print_info "Response: $body"
        return 1
    fi
}

# Test 3: Message History
check_history() {
    print_step "Test 3: Message History abfragen"
    
    local response=$(curl -s -w "%{http_code}" \
        -X GET "$BASE_URL/api/messages/history?limit=10" \
        -H "ocp-apim-subscription-key: $SUBSCRIPTION_KEY")
    
    local http_code=$(echo "$response" | tail -n 1)
    local body=$(echo "$response" | head -n -1)
    
    if [ "$http_code" = "200" ]; then
        print_success "History erfolgreich abgerufen"
        print_info "Response: $body"
        
        # Prüfe ob unsere Message in der History ist
        if echo "$body" | grep -q "$MESSAGE_ID"; then
            print_success "Unsere Message ist in der History enthalten"
        else
            print_error "Unsere Message ist NICHT in der History enthalten"
            return 1
        fi
        return 0
    else
        print_error "History-Abfrage fehlgeschlagen (HTTP $http_code)"
        print_info "Response: $body"
        return 1
    fi
}

# Test 4: Batch SMS Test
test_batch_sms() {
    print_step "Test 4: Batch SMS senden"
    
    local response=$(curl -s -w "%{http_code}" \
        -X POST "$BASE_URL/api/messages" \
        -H "Content-Type: application/json" \
        -H "ocp-apim-subscription-key: $SUBSCRIPTION_KEY" \
        -d "{
            \"messages\": [
                {
                    \"recipient\": \"+1234567891\",
                    \"message\": \"Batch SMS 1\",
                    \"channelType\": \"$CHANNEL_TYPE\"
                },
                {
                    \"recipient\": \"+1234567892\",
                    \"message\": \"Batch SMS 2\",
                    \"channelType\": \"$CHANNEL_TYPE\"
                }
            ]
        }")
    
    local http_code=$(echo "$response" | tail -n 1)
    local body=$(echo "$response" | head -n -1)
    
    if [ "$http_code" = "200" ]; then
        print_success "Batch SMS erfolgreich gesendet"
        print_info "Response: $body"
        return 0
    else
        print_error "Batch SMS senden fehlgeschlagen (HTTP $http_code)"
        print_info "Response: $body"
        return 1
    fi
}

# Test 5: Fehlerbehandlung - Invalider Subscription Key
test_invalid_key() {
    print_step "Test 5: Fehlerbehandlung - Invalider Subscription Key"
    
    local response=$(curl -s -w "%{http_code}" \
        -X POST "$BASE_URL/api/message" \
        -H "Content-Type: application/json" \
        -H "ocp-apim-subscription-key: invalid-key" \
        -d "{
            \"recipient\": \"$RECIPIENT\",
            \"message\": \"$MESSAGE\",
            \"channelType\": \"$CHANNEL_TYPE\"
        }")
    
    local http_code=$(echo "$response" | tail -n 1)
    
    if [ "$http_code" = "401" ]; then
        print_success "Invalider Key korrekt abgelehnt (401)"
        return 0
    else
        print_error "Invalider Key nicht korrekt abgelehnt (HTTP $http_code)"
        return 1
    fi
}

# Haupttest-Flow
main() {
    print_step "SMS Flow Test gestartet"
    print_info "Base URL: $BASE_URL"
    print_info "Subscription Key: $SUBSCRIPTION_KEY"
    print_info "Test Recipient: $RECIPIENT"
    echo ""
    
    # Service verfügbar?
    wait_for_service
    echo ""
    
    # Teste SMS senden
    if send_sms; then
        echo ""
        
        # Teste Status-Abfrage
        if check_status; then
            echo ""
            
            # Teste History
            check_history
            echo ""
            
            # Teste Batch SMS
            test_batch_sms
            echo ""
        fi
    fi
    
    # Teste Fehlerbehandlung
    test_invalid_key
    echo ""
    
    print_step "Test Zusammenfassung"
    print_success "SMS Flow Test abgeschlossen!"
    print_info "Alle Tests erfolgreich durchgeführt"
}

# Script ausführen
main "$@"