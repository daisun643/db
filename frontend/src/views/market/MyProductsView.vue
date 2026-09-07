<template>
  <MyProductsPanel
    :ref="setPanel"
    @edit="openEditProduct"
    @dispute="openDispute"
    @messages="openOrderMessages"
    @error="error = $event"
  />
</template>

<script setup>
import MyProductsPanel from '../../components/market/MyProductsPanel.vue'
import { useMarket } from '../../composables/useMarket'

const { error, registerPanel, openEditProduct, openDispute, openOrderMessages } = useMarket()

// 面板挂载时注册刷新句柄（离开页面时自动注销），供 useMarket 操作后统一刷新
let unregisterPanel = null
const setPanel = (el) => {
  unregisterPanel?.()
  unregisterPanel = el ? registerPanel({
    reloadProducts: () => el.reloadProducts(),
    reloadSales: () => el.reloadSales(),
  }) : null
}
</script>
