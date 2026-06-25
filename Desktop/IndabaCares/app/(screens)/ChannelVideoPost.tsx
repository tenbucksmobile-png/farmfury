/**
 * ChannelVideoPost — PERF-02
 *
 * expo-av is ~800 kB of native bridge code. Keeping it in a separate module
 * lets Metro bundle it independently so the main channel-feed bundle stays lean.
 *
 * Imported via React.lazy() in channel-feed.tsx.
 */

import React, { useState } from 'react';
import {
  View, Pressable, Modal, StyleSheet, Dimensions, TouchableOpacity,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Video, ResizeMode } from 'expo-av';

const SCREEN_WIDTH = Dimensions.get('window').width;

interface Props {
  uri:          string;
  thumbnailUri?: string;
}

export function ChannelVideoPost({ uri, thumbnailUri }: Props) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <TouchableOpacity activeOpacity={0.9} onPress={() => setOpen(true)} style={s.preview}>
        <Video
          source={{ uri: thumbnailUri ?? uri }}
          style={StyleSheet.absoluteFill}
          resizeMode={ResizeMode.COVER}
          shouldPlay={false}
          isMuted
          isLooping={false}
          pointerEvents="none"
        />
        <View style={s.overlay}>
          <View style={s.playBtn}>
            <Ionicons name="play" size={26} color="#fff" />
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
        <Pressable style={s.backdrop} onPress={() => setOpen(false)}>
          <View style={s.lightbox}>
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
          <Pressable style={s.closeBtn} onPress={() => setOpen(false)}>
            <Ionicons name="close" size={22} color="#fff" />
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

export default { ChannelVideoPost };

const VIDEO_HEIGHT = Math.round(SCREEN_WIDTH * 9 / 16);

const s = StyleSheet.create({
  preview: {
    width: '100%',
    height: VIDEO_HEIGHT,
    backgroundColor: '#000',
    overflow: 'hidden',
  },
  overlay: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: 'rgba(0,0,0,0.32)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  playBtn: {
    width: 60,
    height: 60,
    borderRadius: 30,
    backgroundColor: 'rgba(255,255,255,0.22)',
    borderWidth: 2,
    borderColor: 'rgba(255,255,255,0.55)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.78)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  lightbox: {
    width: SCREEN_WIDTH * 0.94,
    aspectRatio: 9 / 16,
    borderRadius: 18,
    overflow: 'hidden',
    backgroundColor: '#000',
    elevation: 12,
  },
  closeBtn: {
    position: 'absolute',
    top: 50,
    right: 20,
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: 'rgba(0,0,0,0.5)',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
