"""
Uploads the App Review screenshot of each of Farm Fury Cannonfire's in-app purchases to App Store Connect
(2026-11-14). Apple only accepts App Store screenshot sizes: iPhone 11 captures (1792x828) were rejected with
IMAGE_INCORRECT_DIMENSIONS, so pass copies scaled to 2688x1242 (the same aspect). One screenshot per Shop page, shared by every product on that page. Products that already have a
review screenshot are skipped, so it can be run again safely.

    python tools/AppStoreConnect/upload_iap_screenshots.py --key-id 7S8MJ6XZ4P --issuer-id <issuer> \
        --key-file ../FarmFury_Secrets/AppStoreConnect/AuthKey_7S8MJ6XZ4P.p8 \
        --dir C:/Users/Personel/Desktop/FarmFury_Technical/AppleScreenshots/Cannonfire [--dry-run]
"""
import argparse
import hashlib
import os
import sys
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import create_iap_products as asc  # noqa: E402  (same folder: Api, find_app, PREFIX, PRODUCTS)

# Shop page screenshot for each product (game ids without the farmfury_ prefix).
PAGE_FOR = {
    "remove_ads": "IMG_2542.png",                       # Shop main page (the Ads tile)
    **{g: "IMG_2543.png" for g in ("coins_100", "coins_500", "coins_5000", "coins_15000")},
    **{g: "IMG_2537.png" for g in ("cannon_golden", "cannon_pumpkin", "cannon_corncob", "cannon_milkchurn",
                                   "cannon_tractor")},
    **{g: "IMG_2538.png" for g in ("trail_rainbowribbon", "trail_sparkledust", "trail_cornhusk", "trail_ember",
                                   "trail_confetti", "trail_bubbles")},
    **{g: "IMG_2539.png" for g in ("impact_confetti", "impact_popcorn", "impact_fireworks", "impact_feathers",
                                   "impact_eggsplat")},
    **{g: "IMG_2544.png" for g in ("world_dustbowlcanyon", "world_harvestfairground", "world_cropfactory")},
}


def upload(api, iap_id, path):
    data = open(path, "rb").read()
    res = api.call("POST", "/v1/inAppPurchaseAppStoreReviewScreenshots", {"data": {
        "type": "inAppPurchaseAppStoreReviewScreenshots",
        "attributes": {"fileName": os.path.basename(path), "fileSize": len(data)},
        "relationships": {"inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": iap_id}}}}})["data"]
    for op in res["attributes"]["uploadOperations"]:
        chunk = data[op["offset"]:op["offset"] + op["length"]]
        req = urllib.request.Request(op["url"], data=chunk, method=op["method"],
                                     headers={h["name"]: h["value"] for h in op.get("requestHeaders", [])})
        urllib.request.urlopen(req).read()
    api.call("PATCH", f"/v1/inAppPurchaseAppStoreReviewScreenshots/{res['id']}", {"data": {
        "type": "inAppPurchaseAppStoreReviewScreenshots", "id": res["id"],
        "attributes": {"uploaded": True, "sourceFileChecksum": hashlib.md5(data).hexdigest()}}})
    return res["id"]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--key-id", required=True)
    p.add_argument("--issuer-id", required=True)
    p.add_argument("--key-file", required=True)
    p.add_argument("--dir", required=True, help="folder holding the Shop page screenshots")
    p.add_argument("--bundle-id", default=asc.BUNDLE_ID)
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args()

    missing = {f for f in PAGE_FOR.values() if not os.path.exists(os.path.join(args.dir, f))}
    if missing:
        sys.exit(f"missing screenshots in {args.dir}: {', '.join(sorted(missing))}")
    unmapped = [g for g, *_ in asc.PRODUCTS if g not in PAGE_FOR]
    if unmapped:
        sys.exit("no screenshot mapped for: " + ", ".join(unmapped))

    api = asc.Api(args.key_id, args.issuer_id, args.key_file)
    app_id = asc.find_app(api, args.bundle_id)
    iaps = {i["attributes"]["productId"]: i["id"]
            for i in api.get_all(f"/v1/apps/{app_id}/inAppPurchasesV2?limit=200")}
    failures = 0
    for gid, *_ in asc.PRODUCTS:
        store_id, shot = asc.PREFIX + gid, PAGE_FOR[gid]
        iap_id = iaps.get(store_id)
        if iap_id is None:
            print(f"  FAIL {store_id:36} not in App Store Connect")
            failures += 1
            continue
        existing = api.call("GET", f"/v2/inAppPurchases/{iap_id}/appStoreReviewScreenshot", allow_404=True)
        if existing and existing.get("data"):
            state = existing["data"]["attributes"].get("assetDeliveryState", {}).get("state")
            if state != "FAILED":
                print(f"  skip {store_id:36} already has one ({state})")
                continue
            if not args.dry_run:   # a rejected upload (e.g. IMAGE_INCORRECT_DIMENSIONS) is replaced
                api.call("DELETE", f"/v1/inAppPurchaseAppStoreReviewScreenshots/{existing['data']['id']}")
        if args.dry_run:
            print(f"  would upload {shot} -> {store_id}")
            continue
        try:
            upload(api, iap_id, os.path.join(args.dir, shot))
            print(f"  OK   {store_id:36} {shot}")
        except (RuntimeError, OSError) as e:
            failures += 1
            print(f"  FAIL {store_id:36} {e}")
    if failures:
        sys.exit(f"{failures} failed; run again to retry (done ones are skipped).")


if __name__ == "__main__":
    main()
