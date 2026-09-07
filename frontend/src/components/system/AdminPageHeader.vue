<template>
  <header class="management-page-header">
    <router-link to="/system-status" class="management-back-link">← 返回系统状态</router-link>
    <span>{{ eyebrow }}</span>
    <h1>{{ title }}</h1>
    <p v-if="description">{{ description }}</p>
  </header>
</template>

<script setup>
defineProps({
  title: { type: String, required: true },
  eyebrow: { type: String, default: '管理工具' },
  description: { type: String, default: '' },
})
</script>
