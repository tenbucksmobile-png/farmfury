import React, { useState } from 'react';
import {
  View, Text, TouchableOpacity,
  StyleSheet, ActivityIndicator,
} from 'react-native';
import { RESPONSE_OPTIONS } from '@/hooks/use-recognition-response';

const PURPLE      = '#7B1FA2';
const PURPLE_SOFT = '#ede9fe';
const PURPLE_TINT = '#ddd6fe';

interface ResponsePickerProps {
  response:    string | null;
  isRecipient: boolean;
  onSelect:    (response: string) => void;
  loading?:    boolean;
}

export function ResponsePicker({
  response,
  isRecipient,
  onSelect,
  loading,
}: ResponsePickerProps) {
  const [open, setOpen] = useState(false);

  // Already responded — show the response for everyone
  if (response) {
    return (
      <View style={s.responseRow}>
        <Text style={s.responseText}>"{response}"</Text>
      </View>
    );
  }

  // Not the recipient — nothing to show
  if (!isRecipient) return null;

  // Options expanded inline
  if (open) {
    return (
      <View style={s.inlineSheet}>
        <View style={s.inlineHeader}>
          <Text style={s.inlineTitle}>Choose a response</Text>
          <TouchableOpacity onPress={() => setOpen(false)} hitSlop={8}>
            <Ionicons name="close" size={18} color="#94a3b8" />
          </TouchableOpacity>
        </View>
        <Text style={s.inlineSub}>+5 pts awarded for responding</Text>

        {RESPONSE_OPTIONS.map((opt, i) => (
          <TouchableOpacity
            key={opt}
            style={[s.option, i === RESPONSE_OPTIONS.length - 1 && s.optionLast]}
            onPress={() => { setOpen(false); onSelect(opt); }}
          >
            <Text style={s.optionText}>{opt}</Text>
          </TouchableOpacity>
        ))}
      </View>
    );
  }

  // Trigger button
  return (
    <TouchableOpacity
      onPress={() => setOpen(true)}
      style={s.triggerBtn}
      disabled={loading}
    >
      {loading ? (
        <ActivityIndicator size="small" color={PURPLE} />
      ) : (
        <Text style={s.triggerText}>Respond</Text>
      )}
    </TouchableOpacity>
  );
}

const s = StyleSheet.create({
  // Existing response
  responseRow: {
    flexDirection: 'row',
    alignItems:    'center',
    gap:           6,
    marginTop:     8,
    alignSelf:     'flex-end',
  },
  responseText: {
    fontSize:   13,
    color:      PURPLE,
    fontWeight: '600',
    fontStyle:  'italic',
  },

  // Trigger
  triggerBtn: {
    flexDirection:     'row',
    alignItems:        'center',
    gap:               5,
    alignSelf:         'flex-end',
    marginTop:         8,
    paddingHorizontal: 10,
    paddingVertical:   5,
    borderRadius:      20,
    backgroundColor:   PURPLE_SOFT,
  },
  triggerText: {
    fontSize:   12,
    fontWeight: '600',
    color:      PURPLE,
  },

  // Inline expanded panel
  inlineSheet: {
    marginTop:        10,
    borderRadius:     14,
    borderWidth:      1.5,
    borderColor:      PURPLE_TINT,
    backgroundColor:  '#faf8ff',
    paddingHorizontal: 14,
    paddingTop:       12,
    paddingBottom:    6,
  },
  inlineHeader: {
    flexDirection:  'row',
    alignItems:     'center',
    justifyContent: 'space-between',
    marginBottom:   2,
  },
  inlineTitle: {
    fontSize:   13,
    fontWeight: '700',
    color:      '#1e1b4b',
  },
  inlineSub: {
    fontSize:     11,
    color:        '#94a3b8',
    marginBottom: 10,
  },
  option: {
    flexDirection:     'row',
    alignItems:        'center',
    gap:               10,
    paddingVertical:   11,
    borderBottomWidth: 1,
    borderBottomColor: '#ede9fe',
  },
  optionLast: {
    borderBottomWidth: 0,
    paddingBottom:     6,
  },
  optionText: {
    fontSize:   13,
    color:      '#1e1b4b',
    fontWeight: '500',
  },
});
