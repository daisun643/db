<template>
  <TabBar :tabs="tabs" :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" />
</template>

<script setup>
import TabBar from '../../tab/TabBar.vue'

defineProps({
  modelValue: { type: String, default: 'all' },
})

defineEmits(['update:modelValue'])

const tabs = [
  { key: 'all', label: '商品列表' },
  { key: 'my-products', label: '我的商品' },
  { key: 'orders', label: '我的订单' },
]
</script>
