"""
Creates Farm Fury Cannonfire's in-app purchases (adapted 2026-11-15 from FarmFury_Stampede/Tools/GooglePlay) in Google Play Console through the Google Play Developer API.

For each product in the catalog it creates the one-time product with its en-US text, one "buy" purchase option
(backwards compatible, so the game's billing library sees its price), prices converted by Google from the USD price
for every region, and then activates it. Run it again at any time: existing products keep their text and prices,
and only a purchase option that isn't active yet is activated. It never deletes anything.

The catalog is PRODUCTS in ../AppStoreConnect/create_iap_products.py, the same list App Store Connect was filled from.

Needs: Python 3.9+ and the 'cryptography' package, an AAB already uploaded to a Play testing track, and a Google
Cloud service account's JSON key with access to this app in Play Console. Never commit the key.

    python tools/GooglePlay/create_play_products.py --key-file ../FarmFury_Secrets/GooglePlay/farm-fury-e4f4026af96c.json --dry-run
    python tools/GooglePlay/create_play_products.py --key-file ../FarmFury_Secrets/GooglePlay/farm-fury-e4f4026af96c.json
"""

import argparse
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "AppStoreConnect"))
from create_iap_products import PREFIX, PRODUCTS  # noqa: E402

API = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/"
SCOPE = "https://www.googleapis.com/auth/androidpublisher"
PACKAGE = "com.farmfury.cannonfire"
OPTION_ID = "buy"
LANGUAGE = "en-US"


def b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def usd_money(usd: str) -> dict:
    """"4.99" -> Google's Money (whole units as a string, the rest in billionths)."""
    units, cents = usd.split(".")
    return {"currencyCode": "USD", "units": units, "nanos": int(cents.ljust(2, "0")) * 10_000_000}


def product_body(package, store_id, name, description, usd, converted):
    """The OneTimeProduct to create, from a convertRegionPrices response."""
    regions = [{"regionCode": code, "price": region["price"], "availability": "AVAILABLE"}
               for code, region in sorted(converted["convertedRegionPrices"].items())]
    # The US shows the catalog's exact price, whatever the conversion rounded to.
    for region in regions:
        if region["regionCode"] == "US":
            region["price"] = usd_money(usd)
    other = converted["convertedOtherRegionsPrice"]
    return {
        "packageName": package,
        "productId": store_id,
        "listings": [{"languageCode": LANGUAGE, "title": name, "description": description}],
        "purchaseOptions": [{
            "purchaseOptionId": OPTION_ID,
            "buyOption": {"legacyCompatible": True},
            "regionalPricingAndAvailabilityConfigs": regions,
            "newRegionsConfig": {"usdPrice": other["usdPrice"], "eurPrice": other["eurPrice"],
                                 "availability": "AVAILABLE"},
        }],
    }


class Api:
    def __init__(self, key_file):
        with open(key_file, encoding="utf-8") as f:
            self.account = json.load(f)
        self.key = serialization.load_pem_private_key(self.account["private_key"].encode(), password=None)
        self.token, self.token_exp = None, 0

    def _token(self):
        now = int(time.time())
        if self.token and now < self.token_exp - 60:
            return self.token
        uri = self.account.get("token_uri", "https://oauth2.googleapis.com/token")
        header = {"alg": "RS256", "typ": "JWT"}
        claims = {"iss": self.account["client_email"], "scope": SCOPE, "aud": uri, "iat": now, "exp": now + 3600}
        signing_input = b64url(json.dumps(header).encode()) + "." + b64url(json.dumps(claims).encode())
        signature = self.key.sign(signing_input.encode(), padding.PKCS1v15(), hashes.SHA256())
        form = urllib.parse.urlencode({"grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer",
                                       "assertion": signing_input + "." + b64url(signature)}).encode()
        with urllib.request.urlopen(urllib.request.Request(uri, data=form, method="POST")) as resp:
            self.token = json.loads(resp.read().decode())["access_token"]
        self.token_exp = now + 3600
        return self.token

    def call(self, method, path, body=None, attempt=0):
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(API + path, data=data, method=method, headers={
            "Authorization": "Bearer " + self._token(), "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req) as resp:
                text = resp.read().decode()
                return json.loads(text) if text else {}
        except urllib.error.HTTPError as e:
            if e.code in (429, 500, 502, 503, 504) and attempt < 4:
                time.sleep(5 * (attempt + 1))
                return self.call(method, path, body, attempt + 1)
            detail = e.read().decode()
            try:
                detail = json.loads(detail)["error"]["message"]
            except (ValueError, KeyError):
                pass
            raise RuntimeError(f"{method} {path} -> HTTP {e.code}: {detail}") from None


def existing_products(api, package):
    products, token = {}, None
    while True:
        query = "?pageSize=1000" + (f"&pageToken={urllib.parse.quote(token)}" if token else "")
        page = api.call("GET", f"{package}/oneTimeProducts{query}")
        for product in page.get("oneTimeProducts", []):
            products[product["productId"]] = product
        token = page.get("nextPageToken")
        if not token:
            return products


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--key-file", required=True, help="the service account's JSON key")
    parser.add_argument("--package", default=PACKAGE)
    parser.add_argument("--dry-run", action="store_true", help="only list what exists and what would be created")
    args = parser.parse_args()

    api = Api(args.key_file)
    existing = existing_products(api, args.package)
    print(f"{args.package}: {len(existing)} one-time product(s) already in Play Console")

    conversions, failures = {}, 0
    for gid, _, usd, name, description in PRODUCTS:
        store_id = PREFIX + gid
        product = existing.get(store_id)
        if args.dry_run:
            print(f"  {'exists ' if product else 'create '} {store_id:36} ${usd:>6}  {name}")
            continue
        try:
            done = []
            if product is None:
                if usd not in conversions:
                    conversions[usd] = api.call("POST", f"{args.package}/pricing:convertRegionPrices",
                                                {"price": usd_money(usd)})
                converted = conversions[usd]
                version = converted["regionVersion"]["version"]
                query = urllib.parse.urlencode({"allowMissing": "true", "updateMask": "listings,purchaseOptions",
                                                "regionsVersion.version": version})
                product = api.call("PATCH", f"{args.package}/onetimeproducts/{store_id}?{query}",
                                   product_body(args.package, store_id, name, description, usd, converted))
                done.append(f"created at ${usd}")
            option = next((o for o in product.get("purchaseOptions", [])
                           if o.get("purchaseOptionId") == OPTION_ID), None)
            if option is not None and option.get("state") != "ACTIVE":
                api.call("POST", f"{args.package}/oneTimeProducts/{store_id}/purchaseOptions:batchUpdateStates",
                         {"requests": [{"activatePurchaseOptionRequest": {
                             "packageName": args.package, "productId": store_id, "purchaseOptionId": OPTION_ID}}]})
                done.append("activated")
            print(f"  OK   {store_id:36} {', '.join(done) or 'already complete'}")
        except RuntimeError as e:
            failures += 1
            print(f"  FAIL {store_id:36} {e}")

    if failures:
        sys.exit(f"{failures} product(s) failed; fix the cause and run again (finished ones are kept).")
    if not args.dry_run:
        print(f"Done: all {len(PRODUCTS)} products are in Play Console.")


if __name__ == "__main__":
    main()
