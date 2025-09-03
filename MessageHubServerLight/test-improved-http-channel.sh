#!/bin/bash

echo "=== Testing Improved HTTP Channel Features ==="

BASE_URL="http://localhost:5000"

# Test 1: Generic payload template (original tenant)
echo "1. Testing Generic Payload Template..."
curl -X POST "$BASE_URL/api/message" \
  -H "Content-Type: application/json" \
  -H "ocp-apim-subscription-key: ocp-apim-subscription-key-local-1" \
  -d '{
    "recipient": "+1234567890",
    "message": "Test Generic Provider",
    "channelType": "HTTP"
  }' | jq '.'

echo -e "\n---\n"

# Test 2: Twilio payload template
echo "2. Testing Twilio Payload Template..."
curl -X POST "$BASE_URL/api/message" \
  -H "Content-Type: application/json" \
  -H "ocp-apim-subscription-key: ocp-apim-subscription-key-local-2" \
  -d '{
    "recipient": "+0987654321",
    "message": "Test Twilio Provider",
    "channelType": "HTTP"
  }' | jq '.'

echo -e "\n---\n"

# Test 3: Custom payload template
echo "3. Testing Custom Payload Template..."
curl -X POST "$BASE_URL/api/message" \
  -H "Content-Type: application/json" \
  -H "ocp-apim-subscription-key: ocp-apim-subscription-key-local-3" \
  -d '{
    "recipient": "+1122334455",
    "message": "Test Custom Provider Template",
    "channelType": "HTTP"
  }' | jq '.'

echo -e "\n---\n"

# Test 4: Rate limiting (send multiple requests quickly)
echo "4. Testing Rate Limiting (sending 10 requests quickly)..."
for i in {1..10}; do
  echo "Request $i:"
  curl -X POST "$BASE_URL/api/message" \
    -H "Content-Type: application/json" \
    -H "ocp-apim-subscription-key: ocp-apim-subscription-key-local-1" \
    -d "{
      \"recipient\": \"+123456789$i\",
      \"message\": \"Rate limit test $i\",
      \"channelType\": \"HTTP\"
    }" | jq -r '.messageId // .error // "No response"'
  sleep 0.1
done

echo -e "\n---\n"

# Test 5: Check message status
echo "5. Checking message status for various messages..."
# First, send a message and get its ID
MESSAGE_RESPONSE=$(curl -s -X POST "$BASE_URL/api/message" \
  -H "Content-Type: application/json" \
  -H "ocp-apim-subscription-key: ocp-apim-subscription-key-local-1" \
  -d '{
    "recipient": "+1234567890",
    "message": "Status check test",
    "channelType": "HTTP"
  }')

MESSAGE_ID=$(echo "$MESSAGE_RESPONSE" | jq -r '.messageId')
echo "Created message ID: $MESSAGE_ID"

# Wait a moment for processing
sleep 2

# Check status
echo "Checking status..."
curl -X GET "$BASE_URL/api/messages/$MESSAGE_ID/status" \
  -H "ocp-apim-subscription-key: ocp-apim-subscription-key-local-1" | jq '.'

echo -e "\n---\n"

# Test 6: Message history
echo "6. Getting message history..."
curl -X GET "$BASE_URL/api/messages/history" \
  -H "ocp-apim-subscription-key: ocp-apim-subscription-key-local-1" | jq '.'

echo -e "\n=== Test Completed ==="