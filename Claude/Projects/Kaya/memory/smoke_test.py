import kaya_memory as km

print("=== Kaya Memory Smoke Test ===")
print(km.remember("The user prefers tea over coffee"))
print(km.remember("The user works from home on Fridays"))
print(km.remember("The user has a dog named Biscuit"))
print()
print("Count:", km.count())
print()
results = km.search("what does the user drink", n=3)
for r in results:
    print(" ", r["score"], r["text"])
print()
print(km.forget("coffee"))
print("Count after forget:", km.count())
