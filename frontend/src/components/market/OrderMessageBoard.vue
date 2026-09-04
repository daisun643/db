<template>
  <section v-if="order" class="orders-panel">
    <h2>订单 #{{ order.transactionID }} 留言板</h2>
    <div class="order-message-list">
      <article v-for="message in messages" :key="message.orderMessageID" class="order-message">
        <strong>{{ message.senderName || '用户' }}</strong>
        <span>{{ formatDate(message.sendTime) }}</span>
        <p>{{ message.content }}</p>
      </article>
      <div v-if="messages.length === 0" class="empty-state compact">
        <p>暂无留言</p>
      </div>
    </div>
    <form v-if="!isOrderArchived(order)" class="message-form" @submit.prevent="$emit('send')">
      <input
        :value="text"
        type="text"
        placeholder="输入订单留言"
        required
        @input="$emit('update:text', $event.target.value)"
      />
      <button class="btn btn-primary" type="submit">发送</button>
    </form>
    <div v-else class="muted">交易已结束，留言板已归档只读。</div>
  </section>
</template>

<script setup>
import { formatDate, isOrderArchived } from '../../composables/useMarket'

defineProps({
  order: { type: Object, default: null },
  messages: { type: Array, default: () => [] },
  text: { type: String, default: '' },
})

defineEmits(['send', 'update:text'])
</script>

<style scoped>
.orders-panel {
  background: linear-gradient(180deg, color-mix(in oklab, var(--surface) 94%, #f8fafc 6%), var(--surface));
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.orders-panel h2 {
  font-size: 1rem;
}

.message-form {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.message-form input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.order-message-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.order-message {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.75rem;
}

.order-message span,
.muted {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.empty-state {
  border: 1px dashed #d9dbe5;
  border-radius: 20px;
  background: #fafaff;
}

.compact {
  min-height: auto;
  padding: 1.5rem;
}
</style>

<style scoped>
/* Marketplace theme */
.orders-panel { border: 1px solid rgba(25,34,59,.08); border-radius: 22px; background: #fff; box-shadow: 0 12px 35px rgba(29,35,58,.05); }
.orders-panel h2 { font-size: 1.15rem; letter-spacing: -.025em; }
.orders-panel .message-form input { border: 1px solid #e3e5ed; border-radius: 12px; background: #fff; transition: border-color .2s, box-shadow .2s; }
.orders-panel .message-form input:focus { border-color: #7463ee; outline: none; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.order-message { border-radius: 14px; background: #f8f8fc; }
</style>
