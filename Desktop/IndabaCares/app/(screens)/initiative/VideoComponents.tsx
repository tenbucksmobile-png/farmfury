/**
 * VideoComponents — PERF-02
 *
 * expo-av is ~800 kB of native bridge code. Keeping it in a separate module
 * lets Metro bundle it independently so it is NOT included in the JS bundle
 * when the user never visits the initiative detail screen.
 *
 * Imported via React.lazy() in [slug].tsx.
 */

import React, { useState } from 'react';
import { View, Pressable, Modal, StyleSheet, Dimensions } from 'react-native';
import { TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Video, ResizeMode } from 'expo-av';

const HALF_SCREEN = Dimensions.get('window').height * 0.45;

// ─── Hero video (half-screen preview → fullscreen lightbox on tap) ────────────

export function VideoHero({ uri }: { uri: string }) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <TouchableOpacity activeOpacity={0.9} onPress={() => setOpen(true)} style={vs.previewWrap}>
        <Video
          source={{ uri }}
          style={StyleSheet.absoluteFill}
          resizeMode={ResizeMode.CONTAIN}
          shouldPlay={false}
          isMuted
          isLooping={false}
          pointerEvents="none"
        />
        <View style={vs.previewOverlay}>
          <View style={vs.playBtn}>
            <Ionicons name="play" size={28} color="#fff" />
          </View>
        </View>
      </TouchableOpacity>

      <Modal
        visible={open}
        transparent
        animationType="fade"
        statusBarTranslucent
        onRequestClose={() => setOpen(false)}
      >
        <Pressable style={vs.overlay} onPress={() => setOpen(false)}>
          <View style={vs.videoLightbox}>
            <Video
              source={{ uri }}
              style={{ flex: 1 }}
              resizeMode={ResizeMode.CONTAIN}
              shouldPlay
              useNativeControls
              isLooping={false}
              onPlaybackStatusUpdate={(status) => {
                if ('didJustFinish' in status && status.didJustFinish) setOpen(false);
              }}
            />
          </View>
        </Pressable>
      </Modal>
    </>
  );
}

// ─── Inline secondary video block ─────────────────────────────────────────────

export function VideoBlock({ uri }: { uri: string }) {
  return (
    <View style={vs.videoWrap}>
      <Video
        source={{ uri }}
        style={vs.video}
        resizeMode={ResizeMode.COVER}
        useNativeControls
        isLooping
      />
    </View>
  );
}

// ─── Default export: combined entry (required for React.lazy) ─────────────────

export default { VideoHero, VideoBlock };

const vs = StyleSheet.create({
  previewWrap: {
    width: '100%',
    height: HALF_SCREEN,
    borderRadius: 18,
    overflow: 'hidden',
    backgroundColor: '#000',
    marginBottom: 16,
  },
  previewOverlay: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: 'rgba(0,0,0,0.35)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  playBtn: {
    width: 64,
    height: 64,
    borderRadius: 32,
    backgroundColor: 'rgba(255,255,255,0.25)',
    borderWidth: 2,
    borderColor: 'rgba(255,255,255,0.6)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.72)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  videoLightbox: {
    width: '92%',
    aspectRatio: 9 / 16,
    borderRadius: 20,
    overflow: 'hidden',
    backgroundColor: '#000',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.4,
    shadowRadius: 20,
    elevation: 12,
  },
  videoWrap: {
    width: '100%',
    aspectRatio: 16 / 9,
    borderRadius: 16,
    overflow: 'hidden',
    backgroundColor: '#000',
    marginBottom: 16,
  },
  video: { flex: 1 },
});
