import requests
import sys

try:
    response = requests.post(
        "http://localhost:8000/v1/text/check",
        json={"text": "I has a apple."}
    )
    print(f"Status Code: {response.status_code}")
    print(response.json())
except Exception as e:
    print(f"Error: {e}")
