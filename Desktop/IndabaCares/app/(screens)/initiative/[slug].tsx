/**
 * Initiative detail screen
 *
 * PERF-02: expo-av is lazy-loaded — it is NOT included in the main JS bundle.
 *           The Video components mount only when this screen is visited AND a
 *           video URI is actually present.
 * PERF-04: RN <Image> replaced with OptimizedImage (expo-image, blur placeholder,
 *           memory-disk cache, fade-in transition).
 */

import React, { useState, Suspense, lazy } from 'react';
import {
  View, Text, ScrollView, FlatList, StyleSheet,
  TouchableOpacity, Pressable, Modal, ActivityIndicator,
} from 'react-native';
import { Image } from 'expo-image';
import { Stack, router, useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useInitiatives } from '@/hooks/use-initiatives';
import { OptimizedImage } from '@/components/ui/OptimizedImage';
import type { Initiative } from '@/api/initiative-service';

const PURPLE      = '#7B1FA2';
const PURPLE_SOFT = '#ede9fe';

// ─── Lazy video (PERF-02) ─────────────────────────────────────────────────────

const LazyVideoBlock = lazy(() =>
  import('./VideoComponents').then((m) => ({ default: m.VideoBlock }))
);

function isVideoUrl(url: string) {
  return /\.(mp4|mov|m4v)(\?|$)/i.test(url);
}

// ─── Types ────────────────────────────────────────────────────────────────────

interface VideoEntry {
  uri:  string;
  date: string;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' });
}

function deriveMedia(data: Initiative[]) {
  const photos: string[]     = [];
  const videos: VideoEntry[] = [];

  for (const item of data) {
    photos.push(...item.image_urls);

    if (item.mascot_url && isVideoUrl(item.mascot_url)) {
      videos.push({ uri: item.mascot_url, date: item.created_at });
    }
    if (item.video_url) {
      videos.push({ uri: item.video_url, date: item.created_at });
    }
  }
  return { photos, videos };
}

// ─── Video list row (X-style) ─────────────────────────────────────────────────

function VideoRow({ entry, title, onPlay }: { entry: VideoEntry; title: string; onPlay: () => void }) {
  return (
    <TouchableOpacity activeOpacity={0.82} onPress={onPlay} style={vs.videoRow}>
      {/* Thumbnail */}
      <View style={vs.thumbWrap}>
        <Image
          source={{ uri: entry.uri }}
          style={vs.thumbImg}
          contentFit="cover"
          transition={200}
        />
        {/* Dark overlay */}
        <View style={vs.thumbOverlay} />
        {/* Play icon */}
        <View style={vs.playIcon}>
          <Ionicons name="play" size={18} color="#fff" />
        </View>
        {/* Video badge */}
        <View style={vs.videoBadge}>
          <Ionicons name="videocam" size={10} color="#fff" />
        </View>
      </View>

      {/* Meta */}
      <View style={vs.videoMeta}>
        <Text style={vs.videoTitle} numberOfLines={2}>{title}</Text>
        <Text style={vs.videoDate}>{formatDate(entry.date)}</Text>
      </View>

      <Ionicons name="chevron-forward" size={16} color="#cbd5e1" />
    </TouchableOpacity>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function InitiativeDetailScreen() {
  const { slug, hotel } = useLocalSearchParams<{ slug: string; hotel: string }>();

  const { data = [], isLoading, isError } = useInitiatives(hotel ?? '', slug ?? '');

  const [mediaTab,    setMediaTab]    = useState<'photos' | 'videos'>('photos');
  const [lightboxIdx, setLightboxIdx] = useState<number | null>(null);
  const [playingUri,  setPlayingUri]  = useState<string | null>(null);

  const { photos, videos } = deriveMedia(data);
  const hasPhotos = photos.length > 0;
  const hasVideos = videos.length > 0;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      {/* Header */}
      <View style={styles.header}>
        <View style={styles.titleRow}>
          <TouchableOpacity onPress={() => router.back()} style={styles.backBtn} hitSlop={8}>
            <Ionicons name="arrow-back" size={22} color="#fff" />
          </TouchableOpacity>
          <Text style={styles.title}>{slug}</Text>
          <View style={{ width: 38 }} />
        </View>

        {/* Photos | Videos pill — only shown once data is loaded */}
        {!isLoading && !isError && (hasPhotos || hasVideos) && (
          <View style={styles.pillRow}>
            <View style={styles.pillTrack}>
              {(['photos', 'videos'] as const).map((tab) => {
                const active = mediaTab === tab;
                return (
                  <TouchableOpacity
                    key={tab}
                    activeOpacity={0.75}
                    onPress={() => setMediaTab(tab)}
                    style={[styles.pill, active && styles.pillActive]}
                  >
                    <Ionicons
                      name={tab === 'photos' ? 'images-outline' : 'videocam-outline'}
                      size={14}
                      color={active ? PURPLE : 'rgba(255,255,255,0.75)'}
                    />
                    <Text style={[styles.pillText, active && styles.pillTextActive]}>
                      {tab === 'photos' ? 'Photos' : 'Videos'}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          </View>
        )}
      </View>

      {/* ── Content ──────────────────────────────────────────────────────────── */}

      {isLoading && (
        <View style={styles.emptyState}>
          <ActivityIndicator color={PURPLE} size="large" />
        </View>
      )}

      {isError && (
        <View style={styles.emptyState}>
          <Ionicons name="alert-circle-outline" size={48} color={PURPLE_SOFT} />
          <Text style={styles.emptyTitle}>Failed to load</Text>
          <Text style={styles.emptyText}>Please check your connection and try again.</Text>
        </View>
      )}

      {!isLoading && !isError && !hasPhotos && !hasVideos && (
        <View style={styles.emptyState}>
          <Ionicons name="ribbon-outline" size={56} color={PURPLE_SOFT} />
          <Text style={styles.emptyTitle}>Coming Soon</Text>
          <Text style={styles.emptyText}>Content for this initiative will appear here.</Text>
        </View>
      )}

      {/* ── Photos tab ─────────────────────────────────────────────────────── */}
      {!isLoading && !isError && mediaTab === 'photos' && (
        hasPhotos ? (
          <ScrollView contentContainerStyle={styles.body} showsVerticalScrollIndicator={false}>
            <View style={vs.grid}>
              {photos.map((url, i) => (
                <TouchableOpacity
                  key={i}
                  activeOpacity={0.85}
                  onPress={() => setLightboxIdx(i)}
                  style={vs.photoWrap}
                >
                  <OptimizedImage uri={url} style={vs.photo} contentFit="cover" />
                </TouchableOpacity>
              ))}
            </View>
          </ScrollView>
        ) : (
          <View style={styles.emptyState}>
            <Ionicons name="images-outline" size={52} color={PURPLE_SOFT} />
            <Text style={styles.emptyTitle}>No photos yet</Text>
            <Text style={styles.emptyText}>Photos for this event will appear here.</Text>
          </View>
        )
      )}

      {/* ── Videos tab — X-style list ───────────────────────────────────────── */}
      {!isLoading && !isError && mediaTab === 'videos' && (
        hasVideos ? (
          <FlatList
            data={videos}
            keyExtractor={(_, i) => String(i)}
            contentContainerStyle={styles.body}
            showsVerticalScrollIndicator={false}
            ItemSeparatorComponent={() => <View style={vs.rowDivider} />}
            renderItem={({ item }) => (
              <VideoRow
                entry={item}
                title={slug ?? 'Video'}
                onPlay={() => setPlayingUri(item.uri)}
              />
            )}
          />
        ) : (
          <View style={styles.emptyState}>
            <Ionicons name="videocam-outline" size={52} color={PURPLE_SOFT} />
            <Text style={styles.emptyTitle}>No videos yet</Text>
            <Text style={styles.emptyText}>Videos for this event will appear here.</Text>
          </View>
        )
      )}

      {/* ── Photo lightbox ─────────────────────────────────────────────────── */}
      <Modal
        visible={lightboxIdx !== null}
        transparent
        animationType="fade"
        onRequestClose={() => setLightboxIdx(null)}
      >
        <Pressable style={vs.overlay} onPress={() => setLightboxIdx(null)}>
          <View style={vs.lightbox}>
            {lightboxIdx !== null && (
              <OptimizedImage
                uri={photos[lightboxIdx]}
                style={vs.lightboxImage}
                contentFit="cover"
              />
            )}
          </View>
          {/* Nav arrows */}
          {lightboxIdx !== null && lightboxIdx > 0 && (
            <TouchableOpacity
              style={vs.lightboxPrev}
              onPress={(e) => { e.stopPropagation(); setLightboxIdx((i) => (i ?? 0) - 1); }}
            >
              <Ionicons name="chevron-back" size={28} color="#fff" />
            </TouchableOpacity>
          )}
          {lightboxIdx !== null && lightboxIdx < photos.length - 1 && (
            <TouchableOpacity
              style={vs.lightboxNext}
              onPress={(e) => { e.stopPropagation(); setLightboxIdx((i) => (i ?? 0) + 1); }}
            >
              <Ionicons name="chevron-forward" size={28} color="#fff" />
            </TouchableOpacity>
          )}
          {/* Counter */}
          {lightboxIdx !== null && photos.length > 1 && (
            <View style={vs.lightboxCounter}>
              <Text style={vs.lightboxCounterText}>{lightboxIdx + 1} / {photos.length}</Text>
            </View>
          )}
        </Pressable>
      </Modal>

      {/* ── Video player modal ─────────────────────────────────────────────── */}
      <Modal
        visible={playingUri !== null}
        transparent
        animationType="fade"
        onRequestClose={() => setPlayingUri(null)}
      >
        <Pressable style={vs.overlay} onPress={() => setPlayingUri(null)}>
          <View style={vs.videoModal}>
            {/* Close button */}
            <TouchableOpacity
              style={vs.videoModalClose}
              onPress={() => setPlayingUri(null)}
              hitSlop={12}
            >
              <Ionicons name="close-circle" size={32} color="#fff" />
            </TouchableOpacity>

            {playingUri && (
              <Suspense fallback={<ActivityIndicator color="#fff" size="large" />}>
                <LazyVideoBlock uri={playingUri} />
              </Suspense>
            )}
          </View>
        </Pressable>
      </Modal>

    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const vs = StyleSheet.create({
  // Photo grid
  grid:      { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  photoWrap: {
    width: '48.5%',
    aspectRatio: 1,
    borderRadius: 16,
    overflow: 'hidden',
    backgroundColor: '#e5e7eb',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 6,
    elevation: 3,
  },
  photo: { width: '100%', height: '100%' },

  // Photo lightbox
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.88)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  lightbox: {
    width: '88%',
    aspectRatio: 1,
    borderRadius: 20,
    overflow: 'hidden',
  },
  lightboxImage: { width: '100%', height: '100%' },
  lightboxPrev: {
    position: 'absolute',
    left: 16,
    padding: 10,
    backgroundColor: 'rgba(0,0,0,0.4)',
    borderRadius: 24,
  },
  lightboxNext: {
    position: 'absolute',
    right: 16,
    padding: 10,
    backgroundColor: 'rgba(0,0,0,0.4)',
    borderRadius: 24,
  },
  lightboxCounter: {
    position: 'absolute',
    bottom: 40,
    backgroundColor: 'rgba(0,0,0,0.5)',
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 6,
  },
  lightboxCounterText: { color: '#fff', fontSize: 13, fontWeight: '600' },

  // Video list rows (X-style)
  videoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff',
    borderRadius: 16,
    overflow: 'hidden',
    paddingRight: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 3,
  },
  thumbWrap: {
    width: 120,
    height: 80,
    backgroundColor: '#1a0a2e',
    justifyContent: 'center',
    alignItems: 'center',
    position: 'relative',
    flexShrink: 0,
  },
  thumbImg: {
    width: '100%',
    height: '100%',
  },
  thumbOverlay: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: 'rgba(0,0,0,0.38)',
  },
  playIcon: {
    position: 'absolute',
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(123,31,162,0.85)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  videoBadge: {
    position: 'absolute',
    bottom: 6,
    left: 6,
    backgroundColor: 'rgba(0,0,0,0.55)',
    borderRadius: 4,
    paddingHorizontal: 4,
    paddingVertical: 2,
  },
  videoMeta: { flex: 1, paddingHorizontal: 14, gap: 6 },
  videoTitle: { fontSize: 14, fontWeight: '700', color: '#1e293b', lineHeight: 20 },
  videoDate:  { fontSize: 12, color: '#94a3b8' },
  rowDivider: { height: 10 },

  // Video player modal
  videoModal: {
    width: '92%',
    borderRadius: 20,
    overflow: 'hidden',
    backgroundColor: '#000',
    minHeight: 220,
    justifyContent: 'center',
  },
  videoModalClose: {
    position: 'absolute',
    top: 10,
    right: 10,
    zIndex: 10,
  },
});

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F2F2F2' },

  header: {
    backgroundColor: PURPLE,
    paddingHorizontal: 16,
    paddingTop: 14,
    paddingBottom: 20,
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
  },
  titleRow: { flexDirection: 'row', alignItems: 'center', marginBottom: 0 },
  backBtn: {
    width: 38, height: 38,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    flex: 1,
    textAlign: 'center',
    fontSize: 18,
    fontWeight: '700',
    color: '#fff',
  },

  // Pills
  pillRow: { alignItems: 'center', marginTop: 14 },
  pillTrack: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255,255,255,0.15)',
    borderRadius: 24,
    padding: 3,
    gap: 4,
  },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
  },
  pillActive: {
    backgroundColor: '#fff',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.12,
    shadowRadius: 4,
    elevation: 3,
  },
  pillText: {
    fontSize: 13,
    fontWeight: '600',
    color: 'rgba(255,255,255,0.85)',
  },
  pillTextActive: { color: PURPLE },
  pillBadge: {
    backgroundColor: 'rgba(255,255,255,0.25)',
    borderRadius: 10,
    paddingHorizontal: 6,
    paddingVertical: 1,
    minWidth: 20,
    alignItems: 'center',
  },
  pillBadgeActive: { backgroundColor: PURPLE },
  pillBadgeText: { fontSize: 11, fontWeight: '700', color: '#fff' },
  pillBadgeTextActive: { color: '#fff' },

  body: {
    paddingTop: 16,
    paddingHorizontal: 16,
    paddingBottom: 32,
  },

  emptyState: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingTop: 80,
    gap: 14,
  },
  emptyTitle: { fontSize: 20, fontWeight: '700', color: '#1e1b4b' },
  emptyText: {
    fontSize: 14, color: '#94a3b8',
    textAlign: 'center', lineHeight: 22, maxWidth: 260,
  },
});
