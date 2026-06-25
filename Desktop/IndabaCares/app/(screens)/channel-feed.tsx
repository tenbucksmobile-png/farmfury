import React, { Suspense } from 'react';
import {
  View, Text, FlatList, ActivityIndicator,
  StyleSheet, Image, Dimensions, TouchableOpacity,
} from 'react-native';
import { Stack, router, useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useChannelPosts } from '@/hooks/use-channel';
import type { ChannelPost } from '@/api/channel-service';
import { COLORS } from '@/lib/constants';

const PRIMARY     = COLORS.primary;
const SCREEN_WIDTH = Dimensions.get('window').width;
const VIDEO_HEIGHT = Math.round(SCREEN_WIDTH * 9 / 16);

// ─── Lazy-load expo-av (PERF-02) ─────────────────────────────────────────────

const ChannelVideoPost = React.lazy(() =>
  import('./ChannelVideoPost').then((m) => ({ default: m.ChannelVideoPost }))
);

// ─── Post cards ───────────────────────────────────────────────────────────────

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60_000);
  if (m < 1)  return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  if (d < 7)  return `${d}d ago`;
  return new Date(iso).toLocaleDateString();
}

function PhotoCard({ post }: { post: ChannelPost }) {
  return (
    <View style={s.card}>
      <Image
        source={{ uri: post.media_url! }}
        style={s.photo}
        resizeMode="cover"
      />
      {!!post.caption && <Text style={s.caption}>{post.caption}</Text>}
      <Text style={s.timestamp}>{timeAgo(post.created_at)}</Text>
    </View>
  );
}

function VideoCard({ post }: { post: ChannelPost }) {
  return (
    <View style={s.card}>
      <Suspense
        fallback={
          <View style={[s.videoPlaceholder, { height: VIDEO_HEIGHT }]}>
            <ActivityIndicator color={PRIMARY} />
          </View>
        }
      >
        <ChannelVideoPost uri={post.media_url!} thumbnailUri={post.thumbnail_url ?? undefined} />
      </Suspense>
      {!!post.caption && <Text style={s.caption}>{post.caption}</Text>}
      <Text style={s.timestamp}>{timeAgo(post.created_at)}</Text>
    </View>
  );
}

function TextCard({ post }: { post: ChannelPost }) {
  return (
    <View style={[s.card, s.textCard]}>
      <Text style={s.textBody}>{post.caption}</Text>
      <Text style={s.timestamp}>{timeAgo(post.created_at)}</Text>
    </View>
  );
}

function PostCard({ post }: { post: ChannelPost }) {
  if (post.post_type === 'photo') return <PhotoCard post={post} />;
  if (post.post_type === 'video') return <VideoCard post={post} />;
  return <TextCard post={post} />;
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function ChannelFeedScreen() {
  const { hotel } = useLocalSearchParams<{ hotel: string }>();

  const {
    data,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isLoading,
    isError,
    refetch,
  } = useChannelPosts(hotel ?? '');

  const posts = data?.pages.flatMap((p) => p) ?? [];

  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      {/* Header */}
      <View style={s.header}>
        <View style={s.titleRow}>
          <TouchableOpacity onPress={() => router.back()} style={s.backBtn} hitSlop={8}>
            <Ionicons name="arrow-back" size={22} color="#fff" />
          </TouchableOpacity>
          <Text style={s.title} numberOfLines={1}>{hotel}</Text>
          <View style={{ width: 38 }} />
        </View>
        <Text style={s.subtitle}>Channel</Text>
      </View>

      {/* Feed */}
      {isLoading ? (
        <View style={s.center}>
          <ActivityIndicator size="large" color={PRIMARY} />
        </View>
      ) : isError ? (
        <View style={s.center}>
          <Ionicons name="alert-circle-outline" size={48} color="#e2d9f3" />
          <Text style={s.emptyTitle}>Could not load posts</Text>
          <TouchableOpacity onPress={() => refetch()} style={s.retryBtn}>
            <Text style={s.retryText}>Try again</Text>
          </TouchableOpacity>
        </View>
      ) : posts.length === 0 ? (
        <View style={s.center}>
          <Ionicons name="images-outline" size={56} color="#e2d9f3" />
          <Text style={s.emptyTitle}>Nothing here yet</Text>
          <Text style={s.emptyText}>Check back soon — posts will appear here.</Text>
        </View>
      ) : (
        <FlatList
          data={posts}
          keyExtractor={(item) => item.id}
          renderItem={({ item }) => <PostCard post={item} />}
          contentContainerStyle={s.list}
          onEndReached={() => { if (hasNextPage) fetchNextPage(); }}
          onEndReachedThreshold={0.4}
          ListFooterComponent={
            isFetchingNextPage
              ? <ActivityIndicator style={{ marginVertical: 20 }} color={PRIMARY} />
              : null
          }
        />
      )}
    </SafeAreaView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const s = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: '#F2F2F2',
  },

  header: {
    backgroundColor: PRIMARY,
    paddingHorizontal: 16,
    paddingTop: 14,
    paddingBottom: 18,
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 2,
  },
  backBtn: {
    width: 38,
    height: 38,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    flex: 1,
    textAlign: 'center',
    fontSize: 17,
    fontWeight: '700',
    color: '#fff',
  },
  subtitle: {
    textAlign: 'center',
    fontSize: 12,
    color: 'rgba(255,255,255,0.65)',
  },

  list: {
    paddingTop: 12,
    paddingBottom: 40,
    gap: 12,
  },

  card: {
    backgroundColor: '#fff',
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.06,
    shadowRadius: 4,
    elevation: 2,
  },
  textCard: {
    marginHorizontal: 12,
    borderRadius: 16,
    borderLeftWidth: 3,
    borderLeftColor: PRIMARY,
    padding: 16,
  },

  photo: {
    width: '100%',
    height: Math.round(SCREEN_WIDTH * 3 / 4),
  },
  videoPlaceholder: {
    width: '100%',
    backgroundColor: '#000',
    alignItems: 'center',
    justifyContent: 'center',
  },

  caption: {
    fontSize: 14,
    color: '#1e293b',
    lineHeight: 20,
    paddingHorizontal: 14,
    paddingTop: 10,
    paddingBottom: 4,
  },
  textBody: {
    fontSize: 15,
    color: '#1e293b',
    lineHeight: 22,
    marginBottom: 8,
  },
  timestamp: {
    fontSize: 11,
    color: '#94a3b8',
    paddingHorizontal: 14,
    paddingBottom: 10,
  },

  center: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 14,
    paddingHorizontal: 32,
  },
  emptyTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: '#1e1b4b',
  },
  emptyText: {
    fontSize: 14,
    color: '#94a3b8',
    textAlign: 'center',
    lineHeight: 22,
  },
  retryBtn: {
    paddingHorizontal: 24,
    paddingVertical: 10,
    borderRadius: 20,
    backgroundColor: PRIMARY,
  },
  retryText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 14,
  },
});
